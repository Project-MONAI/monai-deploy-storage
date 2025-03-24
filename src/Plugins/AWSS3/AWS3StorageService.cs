/*
 * Copyright 2021-2025 MONAI Consortium
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

        public async Task<Dictionary<string, bool>> VerifyObjectsExistAsync(string bucketName, IReadOnlyList<string> artifactList, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.Null(artifactList, nameof(artifactList));

            var existingObjectsDict = new Dictionary<string, bool>();

            foreach (var artifact in artifactList)
            {
                try
                {
                    var fileObjects = await ListObjectsAsync(bucketName, artifact);
                    var folderObjects = await ListObjectsAsync(bucketName, artifact.EndsWith("/") ? artifact : $"{artifact}/", true);

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
                    _logger.LogError(e.Message);

                    existingObjectsDict.Add(artifact, false);
                }
            }

            return existingObjectsDict;
        }

        public async Task<bool> VerifyObjectExistsAsync(string bucketName, string artifactName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(artifactName, nameof(artifactName));

            var fileObjects = await ListObjectsAsync(bucketName, artifactName);
            var folderObjects = await ListObjectsAsync(bucketName, artifactName.EndsWith("/") ? artifactName : $"{artifactName}/", true);

            if (folderObjects.Any() || fileObjects.Any())
            {
                return true;
            }

            _logger.FileNotFoundError(bucketName, $"{artifactName}");

            return false;
        }
    }
}
