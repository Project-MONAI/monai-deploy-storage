/*
 * Copyright 2021-2024 MONAI Consortium
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Text;
using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio.ApiEndpoints;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;
using Monai.Deploy.Storage.S3Policy;
using Newtonsoft.Json;
using ObjectNotFoundException = Minio.Exceptions.ObjectNotFoundException;

namespace Monai.Deploy.Storage.MinIO
{
    public class MinIoStorageService : IStorageService
    {
        private readonly IMinIoClientFactory _minioClientFactory;
        private readonly IAmazonSecurityTokenServiceClientFactory _amazonSecurityTokenServiceClientFactory;
        private readonly ILogger<MinIoStorageService> _logger;
        private readonly StorageServiceConfiguration _options;

        public string Name => "MinIO Storage Service";

        public MinIoStorageService(IMinIoClientFactory minioClientFactory, IAmazonSecurityTokenServiceClientFactory amazonSecurityTokenServiceClientFactory, IOptions<StorageServiceConfiguration> options, ILogger<MinIoStorageService> logger)
        {
            Guard.Against.Null(options, nameof(options));
            _minioClientFactory = minioClientFactory ?? throw new ArgumentNullException(nameof(minioClientFactory));
            _amazonSecurityTokenServiceClientFactory = amazonSecurityTokenServiceClientFactory ?? throw new ArgumentNullException(nameof(amazonSecurityTokenServiceClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            _options = configuration;
        }

        private void ValidateConfiguration(StorageServiceConfiguration configuration)
        {
            Guard.Against.Null(configuration, nameof(configuration));

            foreach (var key in ConfigurationKeys.RequiredKeys)
            {
                if (!configuration.Settings.ContainsKey(key))
                {
                    throw new ConfigurationException($"{Name} is missing configuration for {key}.");
                }
            }
        }

        #region ServiceAccount

        public async Task CopyObjectAsync(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(sourceBucketName, nameof(sourceBucketName));
            Guard.Against.NullOrWhiteSpace(sourceObjectName, nameof(sourceObjectName));
            Guard.Against.NullOrWhiteSpace(destinationBucketName, nameof(destinationBucketName));
            Guard.Against.NullOrWhiteSpace(destinationObjectName, nameof(destinationObjectName));

            var client = _minioClientFactory.GetObjectOperationsClient();
            await CopyObjectUsingClient(client, sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var client = _minioClientFactory.GetObjectOperationsClient();
            var stream = new MemoryStream();
            await GetObjectUsingClient(client, bucketName, objectName, (s) => s.CopyTo(stream), cancellationToken).ConfigureAwait(false);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public async Task<IList<VirtualFileInfo>> ListObjectsAsync(string bucketName, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));

            var client = _minioClientFactory.GetBucketOperationsClient();
            return await ListObjectsUsingClient(client, bucketName, prefix, recursive, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, bool>> VerifyObjectsExistAsync(string bucketName, IReadOnlyList<string> artifactList, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.Null(artifactList, nameof(artifactList));

            var existingObjectsDict = new Dictionary<string, bool>();
            var exceptions = new List<Exception>();

            foreach (var artifact in artifactList)
            {
                try
                {
                    var fileObjects = await ListObjectsAsync(bucketName, artifact, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var folderObjects = await ListObjectsAsync(bucketName, artifact.EndsWith("/") ? artifact : $"{artifact}/", true, cancellationToken).ConfigureAwait(false);

                    if (!folderObjects.Any() && !fileObjects.Any())
                    {
                        _logger.FileNotFoundError(bucketName, $"{artifact}");

                        existingObjectsDict.Add(artifact, false);
                        continue;
                    }
                    existingObjectsDict.Add(artifact, true);
                }
                catch (Exception e)
                {
                    _logger.VerifyObjectError(bucketName, e);
                    existingObjectsDict.Add(artifact, false);
                    exceptions.Add(e);
                }
            }

            if (exceptions.Any())
            {
                throw new VerifyObjectsException(exceptions, existingObjectsDict);
            }
            return existingObjectsDict;
        }

        public async Task<bool> VerifyObjectExistsAsync(string bucketName, string artifactName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(artifactName, nameof(artifactName));

            try
            {
                var fileObjects = await ListObjectsAsync(bucketName, artifactName, cancellationToken: cancellationToken).ConfigureAwait(false);
                var folderObjects = await ListObjectsAsync(bucketName, artifactName.EndsWith("/") ? artifactName : $"{artifactName}/", true, cancellationToken).ConfigureAwait(false);

                if (folderObjects.Any() || fileObjects.Any())
                {
                    return true;
                }

                _logger.FileNotFoundError(bucketName, $"{artifactName}");

                return false;
            }
            catch (Exception ex)
            {
                _logger.VerifyObjectError(bucketName, ex);
                throw new VerifyObjectsException(ex.Message, ex);
            }
        }

        public async Task PutObjectAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string>? metadata, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));

            var client = _minioClientFactory.GetObjectOperationsClient();
            await PutObjectUsingClient(client, bucketName, objectName, data, size, contentType, metadata, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var client = _minioClientFactory.GetObjectOperationsClient();
            await RemoveObjectUsingClient(client, bucketName, objectName, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectsAsync(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));

            var client = _minioClientFactory.GetObjectOperationsClient();
            await RemoveObjectsUsingClient(client, bucketName, objectNames, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateFolderAsync(string bucketName, string folderPath, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderPath, nameof(folderPath));

            var stubFile = folderPath + "/stubFile.txt";

            var data = Encoding.UTF8.GetBytes("stub file");
            var length = data.Length;
            var stream = new MemoryStream(data);

            await PutObjectAsync(bucketName, stubFile, stream, length, "application/octet-stream", null, cancellationToken).ConfigureAwait(false);
        }

        #endregion ServiceAccount

        #region TemporaryCredentials

        public async Task<Credentials> CreateTemporaryCredentialsAsync(string bucketName, string folderName, int durationSeconds = 3600, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderName, nameof(folderName));

            var policy = PolicyExtensions.ToPolicy(bucketName, folderName);

            var policyString = JsonConvert.SerializeObject(policy, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            _logger.TemporaryCredentialPolicy(policyString);
            var assumeRoleRequest = new AssumeRoleRequest
            {
                DurationSeconds = durationSeconds,
                Policy = policyString
            };

            var client = _amazonSecurityTokenServiceClientFactory.GetClient();
            var role = await client.AssumeRoleAsync(assumeRoleRequest, cancellationToken: cancellationToken).ConfigureAwait(false);

            return role.Credentials;
        }

        public async Task CopyObjectWithCredentialsAsync(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(sourceBucketName, nameof(sourceBucketName));
            Guard.Against.NullOrWhiteSpace(sourceObjectName, nameof(sourceObjectName));
            Guard.Against.NullOrWhiteSpace(destinationBucketName, nameof(destinationBucketName));
            Guard.Against.NullOrWhiteSpace(destinationObjectName, nameof(destinationObjectName));

            var client = _minioClientFactory.GetObjectOperationsClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await CopyObjectUsingClient(client, sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Stream> GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var client = _minioClientFactory.GetObjectOperationsClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            var stream = new MemoryStream();
            await GetObjectUsingClient(client, bucketName, objectName, (s) => s.CopyTo(stream), cancellationToken).ConfigureAwait(false);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public async Task<IList<VirtualFileInfo>> ListObjectsWithCredentialsAsync(string bucketName, Credentials credentials, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));

            var client = _minioClientFactory.GetBucketOperationsClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            return await ListObjectsUsingClient(client, bucketName, prefix, recursive, cancellationToken).ConfigureAwait(false);
        }

        public async Task PutObjectWithCredentialsAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));

            var client = _minioClientFactory.GetObjectOperationsClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await PutObjectUsingClient(client, bucketName, objectName, data, size, contentType, metadata, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var client = _minioClientFactory.GetObjectOperationsClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await RemoveObjectUsingClient(client, bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectsWithCredentialsAsync(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));

            var client = _minioClientFactory.GetObjectOperationsClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await RemoveObjectsUsingClient(client, bucketName, objectNames, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateFolderWithCredentialsAsync(string bucketName, string folderPath, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderPath, nameof(folderPath));

            var stubFile = folderPath + "/stubFile.txt";

            var data = Encoding.UTF8.GetBytes("stub file");
            var length = data.Length;
            var stream = new MemoryStream(data);

            var client = _minioClientFactory.GetObjectOperationsClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await PutObjectUsingClient(client, bucketName, stubFile, stream, length, "application/octet-stream", null, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        #endregion TemporaryCredentials

        #region Internal Helper Methods

        private async Task CopyObjectUsingClient(IObjectOperations client, string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, CancellationToken cancellationToken)
        {
            await CallApi(async () =>
            {
                try
                {
                    var copySourceObjectArgs = new CopySourceObjectArgs()
                                   .WithBucket(sourceBucketName)
                                   .WithObject(sourceObjectName);
                    var copyObjectArgs = new CopyObjectArgs()
                        .WithBucket(destinationBucketName)
                        .WithObject(destinationObjectName)
                        .WithCopyObjectSource(copySourceObjectArgs);
                    await client.CopyObjectAsync(copyObjectArgs, cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectNotFoundException ex) when (ex.ServerMessage.Contains("Not found", StringComparison.OrdinalIgnoreCase))
                {
                    throw new API.StorageObjectNotFoundException(ex.ServerMessage);
                }
            }).ConfigureAwait(false);
        }

        private async Task GetObjectUsingClient(IObjectOperations client, string bucketName, string objectName, Action<Stream> callback, CancellationToken cancellationToken)
        {
            await CallApi(async () =>
            {
                var args = new GetObjectArgs()
                                .WithBucket(bucketName)
                                .WithObject(objectName)
                                .WithCallbackStream(callback);
                await client.GetObjectAsync(args, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private Task<IList<VirtualFileInfo>> ListObjectsUsingClient(IBucketOperations client, string bucketName, string? prefix, bool recursive, CancellationToken cancellationToken)
        {
            var files = new List<VirtualFileInfo>();
            var listArgs = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix)
                .WithRecursive(recursive);

            try
            {
                var done = new TaskCompletionSource<IList<VirtualFileInfo>>();

                var objservable = client.ListObjectsAsync(listArgs, cancellationToken);
                var completedEvent = new ManualResetEventSlim(false);
                objservable.Subscribe(item =>
                {
                    if (!item.IsDir)
                    {
                        files.Add(new VirtualFileInfo(Path.GetFileName(item.Key), item.Key, item.ETag, item.Size)
                        {
                            LastModifiedDateTime = item.LastModifiedDateTime
                        });
                    }
                },
                error =>
                {
                    _logger.ListObjectError(bucketName, error.Message);
                    if (error is OperationCanceledException)
                        done.SetException(error);
                    else
                        done.SetException(new ListObjectException(error.ToString()));
                },
                () =>
                {
                    done.SetResult(files);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new ListObjectTimeoutException("Timed out waiting for results.");
                    }
                }, cancellationToken);

                return done.Task;
            }
            catch (ConnectionException ex)
            {
                _logger.ConnectionError(ex);
                var iex = new StorageConnectionException(ex.Message);
                iex.Errors.Add(ex.ServerMessage);
                if (ex.ServerResponse is not null && !string.IsNullOrWhiteSpace(ex.ServerResponse.ErrorMessage))
                {
                    iex.Errors.Add(ex.ServerResponse.ErrorMessage);
                }
                throw iex;
            }
            catch (Exception ex) when (ex is not ListObjectTimeoutException && ex is not ListObjectException)
            {
                _logger.StorageServiceError(ex);
                throw new StorageServiceException(ex.ToString());
            }
        }

        private async Task RemoveObjectUsingClient(IObjectOperations client, string bucketName, string objectName, CancellationToken cancellationToken)
        {
            await CallApi(async () =>
            {
                var args = new RemoveObjectArgs()
                           .WithBucket(bucketName)
                           .WithObject(objectName);
                await client.RemoveObjectAsync(args, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task PutObjectUsingClient(IObjectOperations client, string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string>? metadata, CancellationToken cancellationToken)
        {
            await CallApi(async () =>
            {
                var args = new PutObjectArgs()
                                    .WithBucket(bucketName)
                                    .WithObject(objectName)
                                    .WithStreamData(data)
                                    .WithObjectSize(size)
                                    .WithContentType(contentType);
                if (metadata is not null)
                {
                    args.WithHeaders(metadata);
                }

                await client.PutObjectAsync(args, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task RemoveObjectsUsingClient(IObjectOperations client, string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken)
        {
            await CallApi(async () =>
            {
                var args = new RemoveObjectsArgs()
                               .WithBucket(bucketName)
                               .WithObjects(objectNames.ToList());
                await client.RemoveObjectsAsync(args, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task CallApi(Func<Task> func)
        {
            try
            {
                await func().ConfigureAwait(false);
            }
            catch (ConnectionException ex)
            {
                _logger.ConnectionError(ex);
                var iex = new StorageConnectionException(ex.Message);
                iex.Errors.Add(ex.ServerMessage);
                if (ex.ServerResponse is not null && !string.IsNullOrWhiteSpace(ex.ServerResponse.ErrorMessage))
                {
                    iex.Errors.Add(ex.ServerResponse.ErrorMessage);
                }
                throw iex;
            }
            catch (Exception ex)
            {
                _logger.StorageServiceError(ex);
                throw new StorageServiceException(ex.ToString());
            }
        }

        #endregion Internal Helper Methods
    }
}
