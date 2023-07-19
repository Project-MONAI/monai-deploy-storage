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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.Configuration;
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.AzureBlob.Tests.Integration
{
    [CollectionDefinition("AzureBlobStorage")]
    public class AzureBlobStorageCollection : ICollectionFixture<AzureBlobStorageFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class AzureBlobStorageFixture : IAsyncDisposable
    {
        const int MaxFileSize = 104857600;
        private readonly Random _random;
        private readonly List<string> _files;

        public IOptions<StorageServiceConfiguration> Configurations { get; }
        public AzureBlobClientFactory ClientFactory { get; }
        public IReadOnlyList<string> Files { get => _files; }
        public string ContainerName { get; }

        public AzureBlobStorageFixture()
        {
            _random = new Random();
            _files = new List<string>();

            Configurations = Options.Create(new StorageServiceConfiguration());
            Configurations.Value.Settings.Add(ConfigurationKeys.ConnectionString, "UseDevelopmentStorage=true");

            ClientFactory = new AzureBlobClientFactory(Configurations, new Mock<ILogger<AzureBlobClientFactory>>().Object);
            ContainerName = $"md-test-{_random.Next(1000)}";
        }

        internal async Task GenerateAndUploadData()
        {
            await GenerateAndUploadFile($"{Guid.NewGuid()}").ConfigureAwait(false);
            await GenerateAndUploadFile($"dir-1/{Guid.NewGuid()}").ConfigureAwait(false);
            await GenerateAndUploadFile($"dir-2/{Guid.NewGuid()}").ConfigureAwait(false);
            await GenerateAndUploadFile($"dir-2/a/b/{Guid.NewGuid()}").ConfigureAwait(false);
        }

        private async Task GenerateAndUploadFile(string filePath)
        {
            var data = GetRandomBytes();
            var stream = new MemoryStream(data);
            var client = ClientFactory.GetBlobClient(ContainerName, filePath);
            await client.UploadAsync(stream).ConfigureAwait(false);
            _files.Add(filePath);
        }

        public byte[] GetRandomBytes()
        {
            return new byte[_random.Next(1, MaxFileSize)];
        }

        public async ValueTask DisposeAsync()
        {
            await RemoveData().ConfigureAwait(false);
            await RemoveBucket().ConfigureAwait(false);
        }

        private async Task RemoveBucket()
        {
            var client = ClientFactory.GetBlobContainerClient(ContainerName);
            var exists = await client.ExistsAsync().ConfigureAwait(false);
            if (exists)
            {
                var resultSegment = client.GetBlobsAsync(prefix: ContainerName).AsPages(default, 100);

                await foreach (var blobPage in resultSegment)
                {
                    foreach (var blobItem in blobPage.Values)
                    {
                        await ClientFactory.GetBlobClient(ContainerName, blobItem.Name).DeleteAsync().ConfigureAwait(false);
                    };
                }
            }
        }

        private async Task RemoveData()
        {
            await RemoveBucket().ConfigureAwait(false);
        }
    }
}
