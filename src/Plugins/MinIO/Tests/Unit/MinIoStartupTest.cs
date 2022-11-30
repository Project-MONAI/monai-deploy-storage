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
using Minio;
using Monai.Deploy.Storage.Configuration;
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.MinIO.Tests.Unit
{
    public class MinIoStartupTest
    {
        private readonly Mock<IMinIoClientFactory> _minIoClientFactory;
        private readonly IOptions<StorageServiceConfiguration> _options;
        private readonly Mock<ILogger<MinIoStartup>> _logger;
        private readonly Mock<IBucketOperations> _minio;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public MinIoStartupTest()
        {
            _minIoClientFactory = new Mock<IMinIoClientFactory>();
            _options = Options.Create(new StorageServiceConfiguration());
            _logger = new Mock<ILogger<MinIoStartup>>();
            _minio = new Mock<IBucketOperations>();
            _cancellationTokenSource = new CancellationTokenSource();

            _minIoClientFactory.Setup(p => p.GetBucketOperationsClient()).Returns(_minio.Object);
            _options.Value.Settings[ConfigurationKeys.EndPoint] = "localhost";
            _options.Value.Settings[ConfigurationKeys.AccessKey] = "key";
            _options.Value.Settings[ConfigurationKeys.AccessToken] = "token";
            _options.Value.Settings[ConfigurationKeys.Region] = "region";
        }

        [Fact]
        public void GivenNoCreateBucketKey_WhenStartupIsCalled_ItShallNotCreateBuckets()
        {
            var service = new MinIoStartup(_minIoClientFactory.Object, _options, _logger.Object);

            var task = service.StartAsync(_cancellationTokenSource.Token);

            Assert.True(task.IsCompletedSuccessfully);
            _minIoClientFactory.Verify(p => p.GetBucketOperationsClient(), Times.Never());
        }

        [Fact]
        public void GivenCreateBucketKeyWithNoValue_WhenStartupIsCalled_ItShallNotCreateBuckets()
        {
            _options.Value.Settings[ConfigurationKeys.CreateBuckets] = string.Empty;
            var service = new MinIoStartup(_minIoClientFactory.Object, _options, _logger.Object);

            var task = service.StartAsync(_cancellationTokenSource.Token);

            Assert.True(task.IsCompletedSuccessfully);
            _minIoClientFactory.Verify(p => p.GetBucketOperationsClient(), Times.Never());
        }

        [Fact]
        public async Task GivenCreateBucketKeyValues_WhenStartupIsCalled_ItShallThrowExceptionWhenUnableToCheckBucketsExistence()
        {
            var bucket = "mybucket";
            _options.Value.Settings[ConfigurationKeys.CreateBuckets] = bucket;
            _minio.Setup(p => p.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>())).Throws(new Exception("error"));

            var service = new MinIoStartup(_minIoClientFactory.Object, _options, _logger.Object);

            var task = service.StartAsync(_cancellationTokenSource.Token);
            var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
            {
                await task;
            });

            Assert.NotNull(exception);
            Assert.True(task.IsFaulted);
            _minIoClientFactory.Verify(p => p.GetBucketOperationsClient(), Times.Once());
            _minio.Verify(p => p.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()), Times.Once());
            _minio.Verify(p => p.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task GivenCreateBucketKeyValues_WhenStartupIsCalled_ItShallThrowExceptionWhenUnableToCreateBuckets()
        {
            var bucket = "my-bucket,your-bucket";
            _options.Value.Settings[ConfigurationKeys.CreateBuckets] = bucket;
            _minio.Setup(p => p.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>())).Throws(new Exception("error"));

            var service = new MinIoStartup(_minIoClientFactory.Object, _options, _logger.Object);

            var task = service.StartAsync(_cancellationTokenSource.Token);
            var exceptions = await Assert.ThrowsAsync<AggregateException>(async () =>
            {
                await task;
            });

            Assert.Equal(2, exceptions.InnerExceptions.Count);
            Assert.True(task.IsFaulted);
            _minIoClientFactory.Verify(p => p.GetBucketOperationsClient(), Times.Once());
            _minio.Verify(p => p.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _minio.Verify(p => p.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Theory]
        [InlineData("bucket1", 1)]
        [InlineData("bucket1,bucket2", 2)]
        [InlineData("bucket1,bucket2,,bucket3", 3)]
        public async Task GivenCreateBucketKeyValues_WhenStartupIsCalled_ItShallCreeateBuckets(string buckets, int count)
        {
            _options.Value.Settings[ConfigurationKeys.CreateBuckets] = buckets;
            var service = new MinIoStartup(_minIoClientFactory.Object, _options, _logger.Object);

            var task = service.StartAsync(_cancellationTokenSource.Token);
            await task;

            Assert.True(task.IsCompletedSuccessfully);
            _minIoClientFactory.Verify(p => p.GetBucketOperationsClient(), Times.Once());
            _minio.Verify(p => p.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()), Times.Exactly(count));
            _minio.Verify(p => p.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()), Times.Exactly(count));
        }

        [Theory]
        [InlineData("bucket1", 1)]
        [InlineData("bucket1,bucket2", 2)]
        [InlineData("bucket1,bucket2,,bucket3", 3)]
        public async Task GivenCreateBucketKeyValuesWithoutRegion_WhenStartupIsCalled_ItShallCreeateBuckets(string buckets, int count)
        {
            _options.Value.Settings[ConfigurationKeys.CreateBuckets] = buckets;
            _options.Value.Settings.Remove(ConfigurationKeys.Region);

            var service = new MinIoStartup(_minIoClientFactory.Object, _options, _logger.Object);

            var task = service.StartAsync(_cancellationTokenSource.Token);
            await task;

            Assert.True(task.IsCompletedSuccessfully);
            _minIoClientFactory.Verify(p => p.GetBucketOperationsClient(), Times.Once());
            _minio.Verify(p => p.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()), Times.Exactly(count));
            _minio.Verify(p => p.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()), Times.Exactly(count));
        }
    }
}
