// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Common;
using Monai.Deploy.Storage.Configuration;
using Monai.Deploy.Storage.S3Policy;
using Newtonsoft.Json;

namespace Monai.Deploy.Storage.AWSS3
{
    public class Awss3StorageService : IStorageService
    {
        private readonly ILogger<Awss3StorageService> _logger;
        private readonly AmazonS3Client _client;
        private readonly AmazonSecurityTokenServiceClient _tokenServiceClient;
        private readonly StorageServiceConfiguration _options;

        public string Name => "AWS S3 Storage Service";

        public Awss3StorageService(IOptions<StorageServiceConfiguration> options, ILogger<Awss3StorageService> logger)
        {
            Guard.Against.Null(options, nameof(options));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            _options = configuration;

            var accessKey = configuration.Settings[ConfigurationKeys.AccessKey];
            var accessToken = configuration.Settings[ConfigurationKeys.AccessToken];
            var region = configuration.Settings[ConfigurationKeys.Region];
            var credentialServiceUrl = configuration.Settings[ConfigurationKeys.CredentialServiceUrl];

            _client = new AmazonS3Client(accessKey, accessToken, RegionEndpoint.GetBySystemName(region));
            var config = new AmazonSecurityTokenServiceConfig
            {
                AuthenticationRegion = RegionEndpoint.USEast1.SystemName,
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

        public async Task CopyObjectAsync(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(sourceBucketName, nameof(sourceBucketName));
            Guard.Against.NullOrWhiteSpace(sourceObjectName, nameof(sourceObjectName));
            Guard.Against.NullOrWhiteSpace(destinationBucketName, nameof(destinationBucketName));
            Guard.Against.NullOrWhiteSpace(destinationObjectName, nameof(destinationObjectName));

            await _client.CopyObjectAsync(sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task GetObjectAsync(string bucketName, string objectName, Action<Stream> callback, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(callback, nameof(callback));

            using (var obj = await _client.GetObjectAsync(bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                callback(obj.ResponseStream);
            }
        }

        public async Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var obj = await _client.GetObjectAsync(bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);

            return obj.ResponseStream;
        }

        public async Task<IList<VirtualFileInfo>> ListObjectsAsync(string bucketName, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));

            var request = new ListObjectsV2Request { BucketName = bucketName, Prefix = prefix };
            var files = new List<VirtualFileInfo>();

            ListObjectsV2Response response;

            do
            {
                response = await _client.ListObjectsV2Async(request, cancellationToken);

                response.S3Objects.ForEach(obj => files.Add(new VirtualFileInfo(Path.GetFileName(obj.Key), obj.Key, obj.ETag, (ulong)obj.Size)));
                request.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);

            return files;
        }

        public async Task PutObjectAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));

            var por = new PutObjectRequest
            {
                BucketName = bucketName, Key = objectName, InputStream = data
            };

            await _client.PutObjectAsync(por, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            var dor = new DeleteObjectRequest { BucketName = bucketName, Key = objectName };

            await _client.DeleteObjectAsync(dor, cancellationToken);
        }

        public async Task RemoveObjectsAsync(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));

            var KeyVersionList = new List<KeyVersion>();
            foreach (var objectName in objectNames)
            {
                var keyv = new KeyVersion
                {
                    Key = objectName
                };
                KeyVersionList.Add(keyv);
            }

            var dor = new DeleteObjectsRequest { BucketName = bucketName, Objects = KeyVersionList };

            await _client.DeleteObjectsAsync(dor, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateFolderAsync(string bucketName, string folderPath, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderPath, nameof(folderPath));

            var stubFile = folderPath + "/stubFile.txt";

            var por = new PutObjectRequest { BucketName = bucketName, Key = stubFile, ContentBody = "stub file" };

            await _client.PutObjectAsync(por, cancellationToken).ConfigureAwait(false);
        }

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

            var role = await _tokenServiceClient.AssumeRoleAsync(assumeRoleRequest, cancellationToken: cancellationToken);

            return role.Credentials;
        }

        #endregion ServiceAccount

        #region TemporaryCredentials

        public async Task CopyObjectWithCredentialsAsync(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(sourceBucketName, nameof(sourceBucketName));
            Guard.Against.NullOrWhiteSpace(sourceObjectName, nameof(sourceObjectName));
            Guard.Against.NullOrWhiteSpace(destinationBucketName, nameof(destinationBucketName));
            Guard.Against.NullOrWhiteSpace(destinationObjectName, nameof(destinationObjectName));
            IsCredentialsNull(credentials);

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));
            await client.CopyObjectAsync(sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, Action<Stream> callback, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(callback, nameof(callback));

            IsCredentialsNull(credentials);
            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            using (var obj = await client.GetObjectAsync(bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                callback(obj.ResponseStream);
            }

        }

        public async Task<Stream> GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            IsCredentialsNull(credentials);
            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            var obj = await client.GetObjectAsync(bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);

            return obj.ResponseStream;
        }

        public async Task<IList<VirtualFileInfo>> ListObjectsWithCredentialsAsync(string bucketName, Credentials credentials, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            IsCredentialsNull(credentials);

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            var request = new ListObjectsV2Request { BucketName = bucketName, Prefix = prefix };
            var files = new List<VirtualFileInfo>();

            ListObjectsV2Response response;

            do
            {
                response = await client.ListObjectsV2Async(request, cancellationToken);

                response.S3Objects.ForEach(obj => files.Add(new VirtualFileInfo(Path.GetFileName(obj.Key), obj.Key, obj.ETag, (ulong)obj.Size)));
                request.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);

            return files;
        }

        public async Task PutObjectWithCredentialsAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));
            IsCredentialsNull(credentials);

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));
            var por = new PutObjectRequest
            {
                BucketName = bucketName, Key = objectName, InputStream = data
            };

            await client.PutObjectAsync(por, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            IsCredentialsNull(credentials);

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            var dor = new DeleteObjectRequest { BucketName = bucketName, Key = objectName };

            await client.DeleteObjectAsync(dor, cancellationToken);
        }

        public async Task RemoveObjectsWithCredentialsAsync(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            IsCredentialsNull(credentials);
            var keyVersionList = new List<KeyVersion>();
            foreach (var objectName in objectNames)
            {
                var keyVersion = new KeyVersion
                {
                    Key = objectName
                };
                keyVersionList.Add(keyVersion);
            }
            var dor = new DeleteObjectsRequest { BucketName = bucketName, Objects = keyVersionList };

            await client.DeleteObjectsAsync(dor, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateFolderWithCredentialsAsync(string bucketName, string folderPath, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderPath, nameof(folderPath));

            IsCredentialsNull(credentials);
            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            var stubFile = folderPath + "/stubFile.txt";

            var por = new PutObjectRequest { BucketName = bucketName, Key = stubFile, ContentBody = "stub file" };

            await client.PutObjectAsync(por, cancellationToken).ConfigureAwait(false);
        }

        #endregion TemporaryCredentials

        private void IsCredentialsNull(Credentials credentials)
        {
            Guard.Against.Null(credentials, nameof(credentials));
            Guard.Against.NullOrWhiteSpace(credentials.AccessKeyId, nameof(credentials.AccessKeyId));
            Guard.Against.NullOrWhiteSpace(credentials.SecretAccessKey, nameof(credentials.SecretAccessKey));
            Guard.Against.NullOrWhiteSpace(credentials.SessionToken, nameof(credentials.SessionToken));
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
                    var fileObjects = await ListObjectsAsync(bucketName, obj.Value);
                    var folderObjects = await ListObjectsAsync(bucketName, obj.Value.EndsWith("/") ? obj.Value : $"{obj.Value}/", true);

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

        public async Task<KeyValuePair<string, string>> VerifyObjectExistsAsync(string bucketName, KeyValuePair<string, string> objectPair)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.Null(objectPair, nameof(objectPair));

            var fileObjects = await ListObjectsAsync(bucketName, objectPair.Value);
            var folderObjects = await ListObjectsAsync(bucketName, objectPair.Value.EndsWith("/") ? objectPair.Value : $"{objectPair.Value}/", true);

            if (folderObjects.Any() || fileObjects.Any())
            {
                return objectPair;
            }

            _logger.FileNotFoundError(bucketName, $"{objectPair.Value}");

            return default;
        }
    }
}
