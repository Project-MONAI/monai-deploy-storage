/*
 * Copyright 2022 MONAI Consortium
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Diagnostics;
using System.IO.Abstractions;
using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;
using Monai.Deploy.Storage.Minio.Extensions;
using Monai.Deploy.Storage.S3Policy;
using Monai.Deploy.Storage.S3Policy.Policies;

namespace Monai.Deploy.Storage.MinIO
{
    public class StorageAdminService : IStorageAdminService
    {
        private const string UserCommand = "admin user list minio";
        private readonly string _executableLocation;
        private readonly string _serviceName;
        private readonly string _temporaryFilePath;
        private readonly IFileSystem _fileSystem;

        public StorageAdminService(IOptions<StorageServiceConfiguration> options, ILogger<MinIoStorageService> logger, IFileSystem fileSystem)
        {
            Guard.Against.Null(options, nameof(options));
            Guard.Against.Null(logger, nameof(logger));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            _executableLocation = options.Value.Settings[ConfigurationKeys.McExecutablePath];
            _serviceName = options.Value.Settings[ConfigurationKeys.McServiceName];
            _temporaryFilePath = _fileSystem.Path.GetTempPath();
        }

        public async Task<Credentials> CreateUserAsync(string username, AccessPermissions permissions, string[] bucketNames)
        {
            Guard.Against.NullOrWhiteSpace(username, nameof(username));
            Guard.Against.NullOrEmpty(bucketNames, nameof(bucketNames));

            if (await UserAlreadyExistsAsync(username).ConfigureAwait(false))
            {
                throw new InvalidOperationException("User already exists");
            }

            var credentials = new Credentials();
            var userSecretKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            credentials.SecretAccessKey = userSecretKey;
            credentials.AccessKeyId = username;

            var result = await Execute(CreateUserCmd(username, userSecretKey)).ConfigureAwait(false);

            if (!result.Any(r => r.Contains($"Added user `{username}` successfully.")))
            {
                await RemoveUserAsync(username).ConfigureAwait(false);
                throw new InvalidOperationException($"Unknown Output {result.SelectMany(e => e)}");
            }

            var minioPolicies = new List<string>()
            {
                permissions.GetString()
            };

            var policyRequests = bucketNames.Select(
                    bucket => new PolicyRequest(bucket, "")
                ).ToArray();

            await CreatePolicyAsync(policyRequests, username).ConfigureAwait(false);
            var setPolicyResult = await SetPolicyAsync(IdentityType.User, minioPolicies, credentials.AccessKeyId).ConfigureAwait(false);
            if (!setPolicyResult)
            {
                await RemoveUserAsync(username).ConfigureAwait(false);
                throw new InvalidOperationException("Failed to set policy, user has been removed");
            }

            return credentials;
        }

        private async Task<bool> SetPolicyAsync(IdentityType policyType, List<string> policies, string itemName)
        {
            var policiesStr = string.Join(',', policies);
            var setPolicyCmd = $"admin policy set {_serviceName} {policiesStr} {policyType.ToString().ToLower()}={itemName}";
            var result = await Execute(setPolicyCmd).ConfigureAwait(false);

            var expectedResult = $"Policy `{policiesStr}` is set on {policyType.ToString().ToLower()} `{itemName}`";
            if (!result.Any(r => r.Contains(expectedResult)))
            {
                return false;
            }
            return true;
        }

        private async Task RemoveUserAsync(string username)
        {
            var result = await Execute($"admin user remove {_serviceName} {username}").ConfigureAwait(false);

            if (!result.Any(r => r.Contains($"Removed user `{username}` successfully.")))
            {
                throw new InvalidOperationException("Unable to remove user");
            }
        }

        private async Task<List<string>> Execute(string cmd)
        {
            if (cmd.StartsWith("mc"))
            {
                throw new InvalidOperationException($"Incorrect command \"{cmd}\"");
            }

            using (var process = CreateProcess(cmd))
            {
                var (lines, errors) = await RunProcess(process).ConfigureAwait(false);
                if (errors.Any())
                {
                    throw new InvalidOperationException($"Unknown Error {errors.SelectMany(e => e)}");
                }

                return lines;
            }
        }

        private Process CreateProcess(string cmd)
        {
            var startinfo = new ProcessStartInfo()
            {
                FileName = _executableLocation,
                Arguments = cmd,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process()
            {
                StartInfo = startinfo
            };

            return process;
        }

        /// <summary>
        /// Admin policy command requires json file for policy so we create file
        /// and remove it after setting the admin policy for the user.
        /// </summary>
        /// <param name="policyRequests"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task CreatePolicyAsync(PolicyRequest[] policyRequests, string username)
        {
            Guard.Against.NullOrEmpty(policyRequests, nameof(policyRequests));
            Guard.Against.NullOrWhiteSpace(username, nameof(username));

            var userFile = await CreatePolicyFile(policyRequests, username).ConfigureAwait(false);
            var result = await Execute($"admin policy {_serviceName} pol_{username} {username}.json").ConfigureAwait(false);
            if (!result.Any(r => r.Contains($"Added policy `pol_{username}` successfully.")))
            {
                await RemoveUserAsync(username).ConfigureAwait(false);
                File.Delete($"{username}.json");
                throw new InvalidOperationException("Failed to create policy, user has been removed");
            }
            File.Delete(userFile);
        }

        private async Task<string> CreatePolicyFile(PolicyRequest[] policyRequests, string username)
        {
            Guard.Against.NullOrEmpty(policyRequests, nameof(policyRequests));
            Guard.Against.NullOrWhiteSpace(username, nameof(username));

            var policy = PolicyExtensions.ToPolicy(policyRequests);
            var jsonPolicy = policy.ToJson();
            var lines = new List<string>() { jsonPolicy };
            var filename = _fileSystem.Path.Join(_temporaryFilePath, $"{username}.json");
            await _fileSystem.File.WriteAllLinesAsync(filename, lines).ConfigureAwait(false);
            return filename;
        }

        private async Task<bool> UserAlreadyExistsAsync(string username)
        {
            var result = await Execute(UserCommand).ConfigureAwait(false);
            return result.Any(r => r.Contains(username));
        }

        private static void ValidateConfiguration(StorageServiceConfiguration configuration)
        {
            Guard.Against.Null(configuration, nameof(configuration));

            foreach (var key in ConfigurationKeys.McRequiredKeys)
            {
                if (!configuration.Settings.ContainsKey(key))
                {
                    throw new ConfigurationException($"IMinioAdmin Shell is missing configuration for {key}.");
                }
            }
        }

        private string CreateUserCmd(string username, string secretKey) => $"admin user add {_serviceName} {username} {secretKey}";

        private static async Task<(List<string> Output, List<string> Errors)> RunProcess(Process process)
        {
            var output = new List<string>();
            var errors = new List<string>();
            process.Start();
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                if (line == null) continue;
                output.Add(line);
            }
            while (!process.StandardError.EndOfStream)
            {
                var line = process.StandardError.ReadLine();
                if (line == null) continue;
                errors.Add(line);
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            return (output, errors);
        }
    }
}
