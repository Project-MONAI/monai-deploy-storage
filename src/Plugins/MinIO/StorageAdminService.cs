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
using Monai.Deploy.Storage.S3Policy;
using Monai.Deploy.Storage.S3Policy.Policies;

namespace Monai.Deploy.Storage.MinIO
{
    public class StorageAdminService : IStorageAdminService
    {
        private readonly string _executableLocation;
        private readonly string _serviceName;
        private readonly string _temporaryFilePath;
        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly IFileSystem _fileSystem;
        private readonly string _set_connection_cmd;
        private readonly string _get_connections_cmd;
        private readonly string _get_users_cmd;

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
            _endpoint = options.Value.Settings[ConfigurationKeys.EndPoint];
            _accessKey = options.Value.Settings[ConfigurationKeys.AccessKey];
            _secretKey = options.Value.Settings[ConfigurationKeys.AccessToken];
            _set_connection_cmd = $"alias set {_serviceName} http://{_endpoint} {_accessKey} {_secretKey}";
            _get_connections_cmd = "alias list";
            _get_users_cmd = $"admin user list {_serviceName}";
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

        private string CreateUserCmd(string username, string secretKey)
        {
            Guard.Against.NullOrWhiteSpace(username, nameof(username));
            Guard.Against.NullOrWhiteSpace(secretKey, nameof(secretKey));

            return $"admin user add {_serviceName} {username} {secretKey}";
        }

        public async Task<bool> SetPolicyAsync(IdentityType policyType, List<string> policies, string itemName)
        {
            Guard.Against.Null(policyType, nameof(policyType));
            Guard.Against.Null(policies, nameof(policies));
            Guard.Against.NullOrWhiteSpace(itemName, nameof(itemName));

            var policiesStr = string.Join(',', policies);
            var setPolicyCmd = $"admin policy set {_serviceName} {policiesStr} {policyType.ToString().ToLower()}={itemName}";
            var result = await ExecuteAsync(setPolicyCmd).ConfigureAwait(false);

            var expectedResult = $"Policy `{policiesStr}` is set on {policyType.ToString().ToLower()} `{itemName}`";
            if (!result.Any(r => r.Contains(expectedResult)))
            {
                return false;
            }
            return true;
        }

        private async Task<List<string>> ExecuteAsync(string cmd)
        {
            Guard.Against.NullOrWhiteSpace(cmd, nameof(cmd));

            if (cmd.StartsWith("mc"))
            {
                throw new InvalidOperationException($"Incorrect command \"{cmd}\"");
            }

            using (var process = CreateProcess(cmd))
            {
                var (lines, errors) = await RunProcessAsync(process);
                if (errors.Any())
                {
                    throw new InvalidOperationException($"Unknown Error {string.Join("\n", errors)}");
                }

                return lines;
            }
        }

        private static async Task<(List<string> Output, List<string> Errors)> RunProcessAsync(Process process)
        {
            Guard.Against.Null(process, nameof(process));

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

        private Process CreateProcess(string cmd)
        {
            Guard.Against.NullOrWhiteSpace(cmd, nameof(cmd));

            ProcessStartInfo startinfo = new()
            {
                FileName = _executableLocation,
                Arguments = cmd,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process process = new()
            {
                StartInfo = startinfo
            };

            return process;
        }

        public async Task<bool> HasConnectionAsync()
        {
            var result = await ExecuteAsync(_get_connections_cmd).ConfigureAwait(false);
            return result.Any(r => r.Equals(_serviceName));
        }

        public async Task<bool> SetConnectionAsync()
        {
            if (await HasConnectionAsync())
            {
                return true;
            }
            var result = await ExecuteAsync(_set_connection_cmd).ConfigureAwait(false);
            if (result.Any(r => r.Contains($"Added `{_serviceName}` successfully.")))
            {
                return true;
            }
            return false;
        }

        public async Task<bool> UserAlreadyExistsAsync(string username)
        {
            Guard.Against.NullOrWhiteSpace(username, nameof(username));

            var result = await ExecuteAsync(_get_users_cmd).ConfigureAwait(false);
            return result.Any(r => r.Contains(username));
        }

        public async Task RemoveUserAsync(string username)
        {
            Guard.Against.NullOrWhiteSpace(username, nameof(username));

            var result = await ExecuteAsync($"admin user remove {_serviceName} {username}").ConfigureAwait(false);

            if (!result.Any(r => r.Contains($"Removed user `{username}` successfully.")))
            {
                throw new InvalidOperationException("Unable to remove user");
            }
        }

        [Obsolete("CreateUserAsync with bucketNames is deprecated, please use CreateUserAsync with an array of PolicyRequest instead.")]
        public async Task<Credentials> CreateUserAsync(string username, AccessPermissions permissions, string[] bucketNames)
        {
            Guard.Against.NullOrWhiteSpace(username, nameof(username));
            Guard.Against.Null(bucketNames, nameof(bucketNames));

            var policyRequests = new List<PolicyRequest>();

            for (var i = 0; i < bucketNames.Length; i++)
            {
                policyRequests.Add(new PolicyRequest(bucketNames[i], "/*"));
            }

            return await CreateUserAsync(username, policyRequests.ToArray()).ConfigureAwait(false);
        }

        public async Task<Credentials> CreateUserAsync(string username, PolicyRequest[] policyRequests)
        {
            Guard.Against.NullOrWhiteSpace(username, nameof(username));
            Guard.Against.Null(policyRequests, nameof(policyRequests));

            if (!await SetConnectionAsync())
            {
                throw new InvalidOperationException("Unable to set connection for more information, attempt mc alias set {_serviceName} http://{_endpoint} {_accessKey} {_secretKey}");
            }
            if (await UserAlreadyExistsAsync(username))
            {
                throw new InvalidOperationException("User already exists");
            }

            Credentials credentials = new();
            var userSecretKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            credentials.SecretAccessKey = userSecretKey;
            credentials.AccessKeyId = username;

            var result = await ExecuteAsync(CreateUserCmd(username, userSecretKey)).ConfigureAwait(false);

            if (result.Any(r => r.Contains($"Added user `{username}` successfully.")) is false)
            {
                await RemoveUserAsync(username);
                throw new InvalidOperationException($"Unknown Output {string.Join("\n", result)}");
            }


            var policyName = await CreatePolicyAsync(policyRequests.ToArray(), username).ConfigureAwait(false);
            var minioPolicies = new List<string> { policyName };

            var setPolicyResult = await SetPolicyAsync(IdentityType.User, minioPolicies, credentials.AccessKeyId).ConfigureAwait(false);
            if (setPolicyResult is false)
            {
                await RemoveUserAsync(username).ConfigureAwait(false);
                throw new InvalidOperationException("Failed to set policy, user has been removed");
            }

            return credentials;
        }

        /// <summary>
        /// Admin policy command requires json file for policy so we create file
        /// and remove it after setting the admin policy for the user.
        /// </summary>
        /// <param name="policyRequests"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task<string> CreatePolicyAsync(PolicyRequest[] policyRequests, string username)
        {
            Guard.Against.Null(policyRequests, nameof(policyRequests));
            Guard.Against.NullOrWhiteSpace(username, nameof(username));

            var policyFileName = await CreatePolicyFile(policyRequests, username).ConfigureAwait(false);
            var result = await ExecuteAsync($"admin policy add {_serviceName} pol_{username} {policyFileName}").ConfigureAwait(false);
            if (result.Any(r => r.Contains($"Added policy `pol_{username}` successfully.")) is false)
            {
                await RemoveUserAsync(username);
                File.Delete($"{username}.json");
                throw new InvalidOperationException("Failed to create policy, user has been removed");
            }
            File.Delete($"{username}.json");
            return $"pol_{username}";
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
    }
}
