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

using Microsoft.Extensions.Options;
using Minio.ApiEndpoints;
using Minio.DataModel.Args;
using Monai.Deploy.Storage.Configuration;
using Xunit;

namespace Monai.Deploy.Storage.MinIO.Tests.Integration
{
    [CollectionDefinition("MinIoStorage")]
    public class MinIoStorageCollection : ICollectionFixture<MinIoStorageFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class MinIoStorageFixture : IAsyncDisposable
    {
        const int MaxFileSize = 104857600;
        private readonly Random _random;
        private readonly List<string> _files;

        public IOptions<StorageServiceConfiguration> Configurations { get; }
        public IMinIoClientFactory ClientFactory { get; }
        public IReadOnlyList<string> Files { get => _files; }
        public string BucketName { get; }
        public string Location { get; }

        public MinIoStorageFixture()
        {
            _random = new Random();
            _files = new List<string>();

            Configurations = Options.Create(new StorageServiceConfiguration());
            Configurations.Value.Settings.Add(ConfigurationKeys.EndPoint, "localhost:9000");
            Configurations.Value.Settings.Add(ConfigurationKeys.AccessKey, "minioadmin");
            Configurations.Value.Settings.Add(ConfigurationKeys.AccessToken, "minioadmin");
            Configurations.Value.Settings.Add(ConfigurationKeys.SecuredConnection, "false");
            Configurations.Value.Settings.Add(ConfigurationKeys.Region, "heaven");

            ClientFactory = new MinIoClientFactory(Configurations);
            BucketName = $"md-test-{_random.Next(1000)}";
            Location = $"Heaven-L{_random.Next(28)}";
        }

        internal async Task GenerateAndUploadData(IObjectOperations client)
        {
            await GenerateAndUploadFile(client, $"{Guid.NewGuid()}").ConfigureAwait(false);
            await GenerateAndUploadFile(client, $"dir-1/{Guid.NewGuid()}").ConfigureAwait(false);
            await GenerateAndUploadFile(client, $"dir-2/{Guid.NewGuid()}").ConfigureAwait(false);
            await GenerateAndUploadFile(client, $"dir-2/a/b/{Guid.NewGuid()}").ConfigureAwait(false);
        }

        private async Task GenerateAndUploadFile(IObjectOperations client, string filePath)
        {
            var data = GetRandomBytes();
            var stream = new MemoryStream(data);

            var putObjectArgs = new PutObjectArgs()
                    .WithBucket(BucketName)
                    .WithObject(filePath)
                    .WithObjectSize(data.Length)
                    .WithStreamData(stream);
            await client.PutObjectAsync(putObjectArgs).ConfigureAwait(false);

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
            var client = ClientFactory.GetBucketOperationsClient();
            var args = new RemoveBucketArgs()
                    .WithBucket(BucketName);
            await client.RemoveBucketAsync(args).ConfigureAwait(false);
        }

        private async Task RemoveData()
        {
            var client = ClientFactory.GetObjectOperationsClient();
            var args = new RemoveObjectsArgs()
                    .WithBucket(BucketName)
                    .WithObjects(_files);

            await client.RemoveObjectsAsync(args).ConfigureAwait(false);
        }
    }
}
