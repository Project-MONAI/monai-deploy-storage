// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Amazon;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Amazon.S3;
using Amazon.S3.Model;
using Monai.Deploy.Storage.Common;
using Monai.Deploy.Storage.Common.Extensions;
using Monai.Deploy.Storage.Configuration;
using Newtonsoft.Json;
using Monai.Deploy.Storage;

namespace Monai.Deploy.AWSS3
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

            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            _options = configuration;

            var accessKey = configuration.Settings[ConfigurationKeys.AccessKey];
            var accessToken = configuration.Settings[ConfigurationKeys.AccessToken];
            var region = configuration.Settings[ConfigurationKeys.Region];
            var credentialServiceUrl = configuration.Settings[ConfigurationKeys.CredentialServiceUrl];

            _client = new AmazonS3Client(accessKey, accessToken, RegionEndpoint.GetBySystemName(region)) ;
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

            using (GetObjectResponse obj = await _client.GetObjectAsync(bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                callback(obj.ResponseStream);
            }

        }

        public IList<VirtualFileInfo> ListObjects(string bucketName, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));

            var request = new ListObjectsV2Request { BucketName = bucketName, Prefix = prefix };
            var files = new List<VirtualFileInfo>();

            Task<ListObjectsV2Response> objservable = _client.ListObjectsV2Async(request, cancellationToken);
            var response = new ListObjectsV2Response();

            do
            {

                var completedEvent = new ManualResetEventSlim(false);
                objservable.Wait();
                response = objservable.Result;

                response.S3Objects.ForEach(obj => files.Add(new VirtualFileInfo(Path.GetFileName(obj.Key), obj.Key, obj.ETag, (ulong)obj.Size)));
                request.ContinuationToken = response.NextContinuationToken;

            }
            while (response.IsTruncated);

            return files;
        }


        public async Task PutObject(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));

            PutObjectRequest por = new PutObjectRequest { BucketName = bucketName, Key = objectName };
            por.InputStream = data;


            await _client.PutObjectAsync(por, cancellationToken).ConfigureAwait(false);


        }

        public async Task RemoveObject(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));

            DeleteObjectRequest dor = new DeleteObjectRequest { BucketName = bucketName , Key = objectName};

            await _client.DeleteObjectAsync(dor, cancellationToken);

        }

        public async Task RemoveObjects(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));


            List<KeyVersion> KeyVersionList = new List<KeyVersion>();
            foreach (string objectName in objectNames)
            {
                KeyVersion keyv = new KeyVersion();
                keyv.Key = objectName;
                KeyVersionList.Add(keyv);
            }


            DeleteObjectsRequest dor = new DeleteObjectsRequest { BucketName = bucketName, Objects = KeyVersionList };

            await _client.DeleteObjectsAsync(dor, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateFolder(string bucketName, string folderPath, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderPath, nameof(folderPath));

            var stubFile = folderPath + "/stubFile.txt";

            PutObjectRequest por = new PutObjectRequest { BucketName = bucketName, Key = stubFile , ContentBody = "stub file" };

            await _client.PutObjectAsync(por, cancellationToken).ConfigureAwait(false);
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

        #region TemporaryCredentials

        public async Task CopyObjectWithCredentials(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(sourceBucketName, nameof(sourceBucketName));
            Guard.Against.NullOrWhiteSpace(sourceObjectName, nameof(sourceObjectName));
            Guard.Against.NullOrWhiteSpace(destinationBucketName, nameof(destinationBucketName));
            Guard.Against.NullOrWhiteSpace(destinationObjectName, nameof(destinationObjectName));
            IsCredentialsNull(credentials);


            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));
            await client.CopyObjectAsync(sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task GetObjectWithCredentials(string bucketName, string objectName, Credentials credentials, Action<Stream> callback, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(callback, nameof(callback));


            IsCredentialsNull(credentials);
            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            using (GetObjectResponse obj = await _client.GetObjectAsync(bucketName, objectName, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                callback(obj.ResponseStream);
            }

        }

        public IList<VirtualFileInfo> ListObjectsWithCredentials(string bucketName, Credentials credentials, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {

            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            IsCredentialsNull(credentials);

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            var request = new ListObjectsV2Request { BucketName = bucketName, Prefix = prefix };
            var files = new List<VirtualFileInfo>();

            Task<ListObjectsV2Response> objservable = client.ListObjectsV2Async(request, cancellationToken);
            ListObjectsV2Response response;

            do
            {

                objservable.Wait();
                response = objservable.Result;

                response.S3Objects.ForEach(obj => files.Add(new VirtualFileInfo(Path.GetFileName(obj.Key), obj.Key, obj.ETag, (ulong)obj.Size)));
                request.ContinuationToken = response.NextContinuationToken;

            }
            while (response.IsTruncated);

            return files;
        }

        public async Task PutObjectWithCredentials(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default)
        {

            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            Guard.Against.Null(data, nameof(data));
            Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType));
            IsCredentialsNull(credentials);

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));
            PutObjectRequest por = new PutObjectRequest { BucketName = bucketName, Key = objectName };
            por.InputStream = data;

            await client.PutObjectAsync(por, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectWithCredentials(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(objectName, nameof(objectName));
            IsCredentialsNull(credentials);

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            DeleteObjectRequest dor = new DeleteObjectRequest { BucketName = bucketName, Key = objectName };

            await client.DeleteObjectAsync(dor, cancellationToken);
        }

        public async Task RemoveObjectsWithCredentials(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(objectNames, nameof(objectNames));

            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            IsCredentialsNull(credentials);
            List<KeyVersion> KeyVersionList = new List<KeyVersion>();
            foreach (string objectName in objectNames)
            {
                KeyVersion keyv = new KeyVersion();
                keyv.Key = objectName;
                KeyVersionList.Add(keyv);
            }
            DeleteObjectsRequest dor = new DeleteObjectsRequest { BucketName = bucketName, Objects = KeyVersionList };

            await client.DeleteObjectsAsync(dor, cancellationToken).ConfigureAwait(false);

        }

        public async Task CreateFolderWithCredentials(string bucketName, string folderPath, Credentials credentials, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrEmpty(folderPath, nameof(folderPath));


            IsCredentialsNull(credentials);
            var client = new AmazonS3Client(credentials.AccessKeyId, credentials.SecretAccessKey, RegionEndpoint.GetBySystemName(_options.Settings[ConfigurationKeys.Region]));

            var stubFile = folderPath + "/stubFile.txt";

            PutObjectRequest por = new PutObjectRequest { BucketName = bucketName, Key = stubFile, ContentBody = "stub file" };

            await client.PutObjectAsync(por, cancellationToken).ConfigureAwait(false);

        }

        #endregion

        private void IsCredentialsNull(Credentials credentials)
        {
            Guard.Against.Null(credentials, nameof(credentials));
            Guard.Against.NullOrWhiteSpace(credentials.AccessKeyId, nameof(credentials.AccessKeyId));
            Guard.Against.NullOrWhiteSpace(credentials.SecretAccessKey, nameof(credentials.SecretAccessKey));
            Guard.Against.NullOrWhiteSpace(credentials.SessionToken, nameof(credentials.SessionToken));
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
    }
}
