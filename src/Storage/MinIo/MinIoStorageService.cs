// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System.Text;
using Amazon;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Monai.Deploy.Storage.Common;
using Monai.Deploy.Storage.Configuration;
using Newtonsoft.Json;
using Monai.Deploy.Storage.Core.Policies;
using Monai.Deploy.Storage.Core.Extensions;
using Monai.Deploy.Storage.MinioAdmin.Interfaces;

namespace Monai.Deploy.Storage.MinIo
{
    public class MinIoStorageService : IStorageService
    {
        private readonly ILogger<MinIoStorageService> _logger;
        private readonly MinioClient _client;
        private readonly AmazonSecurityTokenServiceClient _tokenServiceClient;
        private readonly StorageServiceConfiguration _options;

        public string Name => "MinIO Storage Service";

        public MinIoStorageService(IOptions<StorageServiceConfiguration> options, ILogger<MinIoStorageService> logger)
        {
            Guard.Against.Null(options, nameof(options));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            _options = configuration;

            var endpoint = configuration.Settings[ConfigurationKeys.EndPoint];
            var accessKey = configuration.Settings[ConfigurationKeys.AccessKey];
            var accessToken = configuration.Settings[ConfigurationKeys.AccessToken];
            var securedConnection = configuration.Settings[ConfigurationKeys.SecuredConnection];
            var credentialServiceUrl = configuration.Settings[ConfigurationKeys.CredentialServiceUrl];

            _client = new MinioClient(endpoint, accessKey, accessToken);

            if (bool.Parse(securedConnection))
            {
                _client.WithSSL();
            }

            var config = new AmazonSecurityTokenServiceConfig
            {
                AuthenticationRegion = RegionEndpoint.EUWest2.SystemName,
                ServiceURL = credentialServiceUrl
            };

            _tokenServiceClient = new AmazonSecurityTokenServiceClient(accessKey, accessToken, config);
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

        public async Task CopyObject(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(sourceBucketName, nameof(sourceBucketName));
            Guard.Against.NullOrWhiteSpace(sourceObjectName, nameof(sourceObjectName));
            Guard.Against.NullOrWhiteSpace(destinationBucketName, nameof(destinationBucketName));
            Guard.Against.NullOrWhiteSpace(destinationObjectName, nameof(destinationObjectName));

            await _client.CopyObjectAsync(sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task GetObject(string bucketName, string objectName, Action<Stream> callback, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(callback, nameof(callback));

            await _client.GetObjectAsync(bucketName, objectName, callback, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IList<VirtualFileInfo> ListObjects(string bucketName, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));

            var files = new List<VirtualFileInfo>();
            var objservable = _client.ListObjectsAsync(bucketName, prefix, recursive, cancellationToken);
            var completedEvent = new ManualResetEventSlim(false);
            objservable.Subscribe<Item>(item =>
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
        }

        public Dictionary<string, string> VerifyObjectsExist(string bucketName, Dictionary<string, string> objectDict)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.Null(objectDict, nameof(objectDict));

            var existingObjectsDict = new Dictionary<string, string>();

            foreach (var obj in objectDict)
            {
                try
                {
                    var fileObjects = ListObjects(bucketName, obj.Value);
                    var folderObjects = ListObjects(bucketName, obj.Value.EndsWith("/") ? obj.Value : $"{obj.Value}/", true);

                    if (!folderObjects.Any() && !fileObjects.Any())
                    {
                        _logger.FileNotFoundError(bucketName, $"{obj.Value}");

                        continue;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);

                    continue;
                }

                existingObjectsDict.Add(obj.Key, obj.Value);
            }

            return existingObjectsDict;
        }

        public KeyValuePair<string, string> VerifyObjectExists(string bucketName, KeyValuePair<string, string> objectPair)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.Null(objectPair, nameof(objectPair));

            var fileObjects = ListObjects(bucketName, objectPair.Value);
            var folderObjects = ListObjects(bucketName, objectPair.Value.EndsWith("/") ? objectPair.Value : $"{objectPair.Value}/", true);

            if (folderObjects.Any() || fileObjects.Any())
            {
                return objectPair;
            }

            _logger.FileNotFoundError(bucketName, $"{objectPair.Value}");

            return default;
        }

        public async Task PutObject(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));

            await _client.PutObjectAsync(bucketName, objectName, data, size, contentType, metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObject(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            await _client.RemoveObjectAsync(bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjects(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));

            await _client.RemoveObjectAsync(bucketName, objectNames, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateFolder(string bucketName, string folderPath, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderPath, nameof(folderPath));

            var stubFile = folderPath + "/stubFile.txt";

            var data = Encoding.UTF8.GetBytes("stub file");
            var length = data.Length;
            var stream = new MemoryStream(data);

            await _client.PutObjectAsync(bucketName, stubFile, stream, length, "application/octet-stream", cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<Credentials> CreateTemporaryCredentials(string bucketName, string folderName, int durationSeconds = 3600, CancellationToken cancellationToken = default)
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

            var role = await _tokenServiceClient.AssumeRoleAsync(assumeRoleRequest, cancellationToken: cancellationToken);

            return role.Credentials;
        }

        #endregion



        public Credentials CreateReadOnlyUser(string username,
            PolicyRequest[] policyRequests, IMinioAdmin minioAdmin)
        {
            return minioAdmin.CreateReadOnlyUser(username, policyRequests);
        }


        #region TemporaryCredentials

        public async Task CopyObjectWithCredentials(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(sourceBucketName, nameof(sourceBucketName));
            Guard.Against.NullOrWhiteSpace(sourceObjectName, nameof(sourceObjectName));
            Guard.Against.NullOrWhiteSpace(destinationBucketName, nameof(destinationBucketName));
            Guard.Against.NullOrWhiteSpace(destinationObjectName, nameof(destinationObjectName));
            IsCredentialsNull(credentials);

            var client = new MinioClient(_options.Settings[ConfigurationKeys.EndPoint], credentials.AccessKeyId, credentials.SecretAccessKey, _options.Settings[ConfigurationKeys.Region], credentials.SessionToken);

            await client.CopyObjectAsync(sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task GetObjectWithCredentials(string bucketName, string objectName, Credentials credentials, Action<Stream> callback, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(callback, nameof(callback));
            IsCredentialsNull(credentials);

            var client = new MinioClient(_options.Settings[ConfigurationKeys.EndPoint], credentials.AccessKeyId, credentials.SecretAccessKey, _options.Settings[ConfigurationKeys.Region], credentials.SessionToken);

            await client.GetObjectAsync(bucketName, objectName, callback, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IList<VirtualFileInfo> ListObjectsWithCredentials(string bucketName, Credentials credentials, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            IsCredentialsNull(credentials);

            var files = new List<VirtualFileInfo>();
            var client = new MinioClient(_options.Settings[ConfigurationKeys.EndPoint], credentials.AccessKeyId, credentials.SecretAccessKey, _options.Settings[ConfigurationKeys.Region], credentials.SessionToken);
            var objservable = client.ListObjectsAsync(bucketName, prefix, recursive, cancellationToken);
            var completedEvent = new ManualResetEventSlim(false);
            objservable.Subscribe<Item>(item =>
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
        }

        public async Task PutObjectWithCredentials(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));
            IsCredentialsNull(credentials);

            var client = new MinioClient(_options.Settings[ConfigurationKeys.EndPoint], credentials.AccessKeyId, credentials.SecretAccessKey, _options.Settings[ConfigurationKeys.Region], credentials.SessionToken);

            await client.PutObjectAsync(bucketName, objectName, data, size, contentType, metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectWithCredentials(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            IsCredentialsNull(credentials);

            await _client.RemoveObjectAsync(bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectsWithCredentials(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));
            IsCredentialsNull(credentials);

            await _client.RemoveObjectAsync(bucketName, objectNames, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateFolderWithCredentials(string bucketName, string folderPath, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderPath, nameof(folderPath));
            IsCredentialsNull(credentials);

            var stubFile = folderPath + "/stubFile.txt";

            var data = Encoding.UTF8.GetBytes("stub file");
            var length = data.Length;
            var stream = new MemoryStream(data);

            var client = new MinioClient(_options.Settings[ConfigurationKeys.EndPoint], credentials.AccessKeyId, credentials.SecretAccessKey, _options.Settings[ConfigurationKeys.Region], credentials.SessionToken);

            await client.PutObjectAsync(bucketName, stubFile, stream, length, "application/octet-stream", cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        #endregion

        private void IsCredentialsNull(Credentials credentials)
        {
            Guard.Against.Null(credentials, nameof(credentials));
            Guard.Against.NullOrWhiteSpace(credentials.AccessKeyId, nameof(credentials.AccessKeyId));
            Guard.Against.NullOrWhiteSpace(credentials.SecretAccessKey, nameof(credentials.SecretAccessKey));
            Guard.Against.NullOrWhiteSpace(credentials.SessionToken, nameof(credentials.SessionToken));
        }
    }
}
