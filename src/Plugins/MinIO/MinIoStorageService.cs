// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System.Text;
using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;
using Monai.Deploy.Storage.S3Policy;
using Newtonsoft.Json;

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

            var client = _minioClientFactory.GetClient();
            await CopyObjectUsingClient(client, sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var stream = new MemoryStream();

            var client = _minioClientFactory.GetClient();
            await GetObjectUsingClient(client, bucketName, objectName, async (s) => await s.CopyToAsync(stream), cancellationToken).ConfigureAwait(false);

            return stream;
        }

        public async Task<IList<VirtualFileInfo>> ListObjectsAsync(string bucketName, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));

            var client = _minioClientFactory.GetClient();
            return await ListObjectsUsingClient(client, bucketName, prefix, recursive, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, string>> VerifyObjectsExistAsync(string bucketName, Dictionary<string, string> objectDict)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.Null(objectDict, nameof(objectDict));

            var existingObjectsDict = new Dictionary<string, string>();

            foreach (var obj in objectDict)
            {
                try
                {
                    var fileObjects = await ListObjectsAsync(bucketName, obj.Value).ConfigureAwait(false);
                    var folderObjects = await ListObjectsAsync(bucketName, obj.Value.EndsWith("/") ? obj.Value : $"{obj.Value}/", true).ConfigureAwait(false);

                    if (!folderObjects.Any() && !fileObjects.Any())
                    {
                        _logger.FileNotFoundError(bucketName, $"{obj.Value}");

                        continue;
                    }
                }
                catch (Exception e)
                {
                    _logger.VerifyObjectError(bucketName, e);

                    continue;
                }

                existingObjectsDict.Add(obj.Key, obj.Value);
            }

            return existingObjectsDict;
        }

        public async Task<KeyValuePair<string, string>> VerifyObjectExistsAsync(string bucketName, KeyValuePair<string, string> objectPair)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.Null(objectPair, nameof(objectPair));

            var fileObjects = await ListObjectsAsync(bucketName, objectPair.Value).ConfigureAwait(false);
            var folderObjects = await ListObjectsAsync(bucketName, objectPair.Value.EndsWith("/") ? objectPair.Value : $"{objectPair.Value}/", true).ConfigureAwait(false);

            if (folderObjects.Any() || fileObjects.Any())
            {
                return objectPair;
            }

            _logger.FileNotFoundError(bucketName, $"{objectPair.Value}");

            return default;
        }

        public async Task PutObjectAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string>? metadata, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));

            var client = _minioClientFactory.GetClient();
            await PutObjectUsingClient(client, bucketName, objectName, data, size, contentType, metadata, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var client = _minioClientFactory.GetClient();
            await RemoveObjectUsingClient(client, bucketName, objectName, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectsAsync(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));

            var client = _minioClientFactory.GetClient();
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

            var client = _minioClientFactory.GetClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await CopyObjectUsingClient(client, sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Stream> GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var stream = new MemoryStream();

            var client = _minioClientFactory.GetClient(credentials, _options.Settings[ConfigurationKeys.Region]);

            await GetObjectUsingClient(client, bucketName, objectName, async (s) => await s.CopyToAsync(stream), cancellationToken).ConfigureAwait(false);

            return stream;
        }

        public async Task<IList<VirtualFileInfo>> ListObjectsWithCredentialsAsync(string bucketName, Credentials credentials, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));

            var client = _minioClientFactory.GetClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            return await ListObjectsUsingClient(client, bucketName, prefix, recursive, cancellationToken).ConfigureAwait(false);
        }

        public async Task PutObjectWithCredentialsAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));

            var client = _minioClientFactory.GetClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await PutObjectUsingClient(client, bucketName, objectName, data, size, contentType, metadata, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var client = _minioClientFactory.GetClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await RemoveObjectUsingClient(client, bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectsWithCredentialsAsync(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));

            var client = _minioClientFactory.GetClient(credentials, _options.Settings[ConfigurationKeys.Region]);
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

            var client = _minioClientFactory.GetClient(credentials, _options.Settings[ConfigurationKeys.Region]);
            await PutObjectUsingClient(client, bucketName, stubFile, stream, length, "application/octet-stream", null, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        #endregion TemporaryCredentials

        #region Internal Helper Methods

        private static async Task CopyObjectUsingClient(MinioClient client, string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, CancellationToken cancellationToken)
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

        private static async Task GetObjectUsingClient(MinioClient client, string bucketName, string objectName, Action<Stream> callback, CancellationToken cancellationToken)
        {
            var args = new GetObjectArgs()
                            .WithBucket(bucketName)
                            .WithObject(objectName)
                            .WithCallbackStream(callback);
            await client.GetObjectAsync(args, cancellationToken).ConfigureAwait(false);
        }

        private async Task<IList<VirtualFileInfo>> ListObjectsUsingClient(MinioClient client, string bucketName, string? prefix, bool recursive, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var files = new List<VirtualFileInfo>();
                var listArgs = new ListObjectsArgs()
                    .WithBucket(bucketName)
                    .WithPrefix(prefix)
                    .WithRecursive(recursive);

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
                    _logger.ListObjectError(bucketName);
                },
                () => completedEvent.Set(), cancellationToken);

                completedEvent.Wait(cancellationToken);
                return files;
            }).ConfigureAwait(false);
        }

        private static async Task RemoveObjectUsingClient(MinioClient client, string bucketName, string objectName, CancellationToken cancellationToken)
        {
            var args = new RemoveObjectArgs()
                           .WithBucket(bucketName)
                           .WithObject(objectName);
            await client.RemoveObjectAsync(args, cancellationToken).ConfigureAwait(false);
        }

        private static async Task PutObjectUsingClient(MinioClient client, string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string>? metadata, CancellationToken cancellationToken)
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
        }

        private static async Task RemoveObjectsUsingClient(MinioClient client, string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken)
        {
            var args = new RemoveObjectsArgs()
                           .WithBucket(bucketName)
                           .WithObjects(objectNames.ToList());
            await client.RemoveObjectsAsync(args, cancellationToken).ConfigureAwait(false);
        }

        #endregion Internal Helper Methods
    }
}
