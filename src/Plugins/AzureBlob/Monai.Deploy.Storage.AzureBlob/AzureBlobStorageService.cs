/*
 * Copyright 2021-2023 MONAI Consortium
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

using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using System.Text;

namespace Monai.Deploy.Storage.AzureBlob
{
    public class AzureBlobStorageService : IStorageService
    {
        private readonly IAzureBlobClientFactory _azureBlobClientFactory;
        private readonly ILogger<AzureBlobStorageService> _logger;
        private readonly StorageServiceConfiguration _options;

        public string Name => "AzureBlob Storage Service";

        public AzureBlobStorageService(IAzureBlobClientFactory azureBlobClientFactory, IOptions<StorageServiceConfiguration> options, ILogger<AzureBlobStorageService> logger)
        {
            Guard.Against.Null(options);
            _azureBlobClientFactory = azureBlobClientFactory ?? throw new ArgumentNullException(nameof(azureBlobClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            _options = configuration;
        }

        private void ValidateConfiguration(StorageServiceConfiguration configuration)
        {
            Guard.Against.Null(configuration);

            foreach (var key in ConfigurationKeys.RequiredKeys)
            {
                if (!configuration.Settings.ContainsKey(key))
                {
                    throw new ConfigurationException($"{Name} is missing configuration for {key}.");
                }
            }
        }

        #region ServiceAccount

        public async Task CopyObjectAsync(string sourcecontainer, string sourceObjectName, string destinationcontainer, string destinationObjectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(sourceObjectName);
            Guard.Against.NullOrWhiteSpace(destinationcontainer);
            Guard.Against.NullOrWhiteSpace(destinationObjectName);

            var source = SanitiseBlobPath(sourcecontainer, sourceObjectName);
            var destination = SanitiseBlobPath(destinationcontainer, destinationObjectName);

            try
            {
                var sourceClient = _azureBlobClientFactory.GetBlobClient(source.container, source.path);
                var destClient = _azureBlobClientFactory.GetBlobBlockClient(destination.container, destination.path);

                var exsists = sourceClient.Exists(cancellationToken);
                if (exsists.Value is false)
                {
                    _logger.FileNotFoundError(sourcecontainer, sourceObjectName);
                    throw new StorageObjectNotFoundException($"Source file {sourceObjectName} does not exist in {sourcecontainer}");
                }

                var blobSasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = source.container,
                    BlobName = source.path,
                    ExpiresOn = DateTime.UtcNow.AddHours(1)
                };

                blobSasBuilder.SetPermissions(BlobSasPermissions.Read);
                var sasToken = sourceClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.Now.AddHours(1));


                await destClient.StartCopyFromUriAsync(sourceClient.Uri  /*, overwrite: false*/, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.BlobCopied(source.container, source.path, destination.container, destination.path);
            }
            catch (Exception ex)
            {
                _logger.StorageServiceError(ex);
                throw new StorageServiceException(ex.Message);
            }
        }

        public async Task<Stream> GetObjectAsync(string container, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(objectName);

            var source = SanitiseBlobPath(container, objectName);
            try
            {
                var client = _azureBlobClientFactory.GetBlobClient(source.container, source.path);

                var stream = new MemoryStream();
                await client.DownloadToAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                stream.Seek(0, SeekOrigin.Begin);
                _logger.BlobGetObject(source.container, source.path);
                return stream;
            }
            catch (Exception ex)
            {
                _logger.StorageServiceError(ex);
                throw new StorageServiceException(ex.Message);
            }
        }

        public async Task<IList<VirtualFileInfo>> ListObjectsAsync(string container, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            var maxSingle = 1000;
            if (string.IsNullOrWhiteSpace(container)) { container = "$root"; }

            var source = SanitiseBlobPath(container, prefix ?? "");
            var folder = Path.GetDirectoryName(source.path) ?? "";

            try
            {
                var client = _azureBlobClientFactory.GetBlobContainerClient(source.container);
                var containerExists = await client.ExistsAsync().ConfigureAwait(false);
                if (containerExists.Value is false)
                {
                    _logger.ContainerDoesNotExistCreated(source.container);
                    return new List<VirtualFileInfo>();
                }
                var resultSegment = client.GetBlobsAsync(prefix: source.path, cancellationToken: cancellationToken).AsPages(default, maxSingle);
                var files = new List<VirtualFileInfo>();

                await foreach (var blobPage in resultSegment)
                {
                    foreach (var blobItem in blobPage.Values)
                    {
                        var file = new VirtualFileInfo(Path.GetFileName(blobItem.Name),
                            blobItem.Name ?? "",
                            string.Empty,
                            (ulong)(blobItem.Properties.ContentLength ?? 0
                        ));

                        if (recursive)
                        {
                            files.Add(file);
                        }
                        else if (file.FilePath == folder)
                        {
                            files.Add(file);
                        }

                    };
                }
                _logger.BlobListObjects(container, prefix);
                return files;
            }
            catch (Exception ex)
            {
                _logger.StorageServiceError(ex);
                throw new StorageServiceException(ex.Message);
            }
        }

        public async Task<Dictionary<string, bool>> VerifyObjectsExistAsync(string container, IReadOnlyList<string> artifactList, CancellationToken cancellationToken = default)
        {
            Guard.Against.Null(artifactList);

            var existingObjectsDict = new Dictionary<string, bool>();
            var exceptions = new List<Exception>();

            foreach (var artifact in artifactList)
            {
                try
                {
                    var source = SanitiseBlobPath(container, artifact);
                    var blobClient = _azureBlobClientFactory.GetBlobClient(source.container, source.path);

                    var exists = await blobClient.ExistsAsync();
                    if (exists.Value is false)
                    {
                        _logger.FileNotFoundError(container, $"{artifact}");

                        existingObjectsDict.Add(artifact, false);
                        continue;
                    }
                    existingObjectsDict.Add(artifact, true);
                }
                catch (Exception e)
                {
                    _logger.VerifyObjectError(container, e);
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

        public async Task<bool> VerifyObjectExistsAsync(string container, string artifactName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(artifactName);

            try
            {
                var source = SanitiseBlobPath(container, artifactName);
                var blobClient = _azureBlobClientFactory.GetBlobClient(source.container, source.path);
                var exists = await blobClient.ExistsAsync();

                if (exists.Value is true)
                {
                    return true;
                }

                _logger.FileNotFoundError(container, $"{artifactName}");

                return false;
            }
            catch (Exception ex)
            {
                _logger.VerifyObjectError(container, ex);
                throw new VerifyObjectsException(ex.Message, ex);
            }
        }

        public async Task PutObjectAsync(string container, string objectName, Stream data, long size, string contentType, Dictionary<string, string>? metadata, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(objectName);
            Guard.Against.Null(data);
            Guard.Against.NullOrWhiteSpace(contentType);

            var source = SanitiseBlobPath(container, objectName);

            try
            {
                var client = _azureBlobClientFactory.GetBlobClient(source.container, source.path);
                var headers = new BlobHttpHeaders { ContentType = contentType };
                await client.UploadAsync(data, overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                await client.SetHttpHeadersAsync(headers, cancellationToken: cancellationToken).ConfigureAwait(false);
                await client.SetMetadataAsync(metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.BlobPutObject(source.container, source.path);
            }
            catch (Exception ex)
            {
                _logger.StorageServiceError(ex);
                throw new StorageServiceException(ex.Message);
            }
        }

        public async Task RemoveObjectAsync(string container, string objectName, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrWhiteSpace(objectName);
            var source = SanitiseBlobPath(container, objectName);

            try
            {
                var client = _azureBlobClientFactory.GetBlobClient(source.container, source.path);
                await client.DeleteAsync().ConfigureAwait(false);
                _logger.BlobRemoveObject(source.container, source.path);
            }
            catch (Exception ex)
            {
                _logger.StorageServiceError(ex);
                //throw new StorageServiceException(ex.Message);
            }
        }

        public async Task RemoveObjectsAsync(string container, IEnumerable<string> objectNames, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrEmpty(objectNames);

            var sanitisedNames = objectNames.Select(name => SanitiseBlobPath(container, name).path);

            try
            {
                var containerClient = _azureBlobClientFactory.GetBlobContainerClient(container);
                var batchClient = new BlobBatchClient(containerClient);

                await batchClient.DeleteBlobsAsync(sanitisedNames.Select(s => new Uri($"{containerClient.Uri}/{s}"))).ConfigureAwait(false);
                _logger.BlobRemoveObjects(container);
            }
            catch (Exception ex)
            {
                _logger.StorageServiceError(ex);
                //throw new StorageServiceException(ex.Message);
            }
        }

        public async Task CreateFolderAsync(string container, string folderPath, CancellationToken cancellationToken = default)
        {
            var containerPath = SanitiseBlobPath(container, folderPath);
            try
            {
                var containerClient = _azureBlobClientFactory.GetBlobContainerClient(containerPath.container);
                await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                var path = $"{containerPath.path}/stubFile.txt".Replace("//", "/");
                var blobClient = _azureBlobClientFactory.GetBlobClient(containerPath.container, path);

                var data = Encoding.UTF8.GetBytes("stub file");
                var length = data.Length;
                var stream = new MemoryStream(data);

                await blobClient.UploadAsync(stream, true, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.ContainerCreated($"{containerPath.container}/{containerPath.path}");
            }
            catch (Exception ex)
            {
                _logger.StorageServiceError(ex);
                throw new StorageServiceException(ex.Message);
            }
        }

        #endregion ServiceAccount

        #region TemporaryCredentials

        public async Task CopyObjectWithCredentialsAsync(string sourcecontainer, string sourceObjectName, string destinationcontainer, string destinationObjectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            await CopyObjectAsync(sourcecontainer, sourceObjectName, destinationcontainer, destinationObjectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<Stream> GetObjectWithCredentialsAsync(string container, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            return await GetObjectAsync(container, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<IList<VirtualFileInfo>> ListObjectsWithCredentialsAsync(string container, Credentials credentials, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            return await ListObjectsAsync(container, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task PutObjectWithCredentialsAsync(string container, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default)
        {
            await PutObjectAsync(container, objectName, data, size, contentType, metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectWithCredentialsAsync(string container, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            await RemoveObjectAsync(container, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveObjectsWithCredentialsAsync(string container, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default)
        {
            await RemoveObjectsAsync(container, objectNames, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateFolderWithCredentialsAsync(string container, string folderPath, Credentials credentials, CancellationToken cancellationToken = default)
        {
            await CreateFolderAsync(container, folderPath, cancellationToken).ConfigureAwait(false);
        }

        #endregion TemporaryCredentials

        public Task<Credentials> CreateTemporaryCredentialsAsync(string container, string folderName, int durationSeconds = 3600, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        private (string container, string path) SanitiseBlobPath(string container, string path)
        {
            if (string.IsNullOrWhiteSpace(container)) { return ("", path); }
            var whole = $"{container}/{path}".Replace("//", "/").Replace("//", "/");
            var indexOf = whole.IndexOf("/");
            var containerBit = container.Substring(0, indexOf);
            var pathBit = whole.Substring(indexOf + 1);
            return (containerBit, pathBit);
        }
    }
}
