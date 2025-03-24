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

using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio.ApiEndpoints;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Result;
using Minio.Exceptions;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.MinIO.Tests.Unit
{
    public class MinIoStorageServiceTest
    {
        private readonly Mock<IMinIoClientFactory> _minIoClientFactory;
        private readonly Mock<IAmazonSecurityTokenServiceClientFactory> _amazonStsClient;
        private readonly Mock<ILogger<MinIoStorageService>> _logger;
        private readonly Mock<IObjectOperations> _objectOperations;
        private readonly Mock<IBucketOperations> _bucketOperations;
        private readonly IOptions<StorageServiceConfiguration> _options;

        public MinIoStorageServiceTest()
        {
            _minIoClientFactory = new Mock<IMinIoClientFactory>();
            _amazonStsClient = new Mock<IAmazonSecurityTokenServiceClientFactory>();
            _logger = new Mock<ILogger<MinIoStorageService>>();
            _objectOperations = new Mock<IObjectOperations>();
            _bucketOperations = new Mock<IBucketOperations>();
            _options = Options.Create<StorageServiceConfiguration>(new StorageServiceConfiguration());

            _minIoClientFactory.Setup(p => p.GetObjectOperationsClient()).Returns(_objectOperations.Object);
            _minIoClientFactory.Setup(p => p.GetBucketOperationsClient()).Returns(_bucketOperations.Object);

            _options.Value.Settings.Add(ConfigurationKeys.EndPoint, "endpoint");
            _options.Value.Settings.Add(ConfigurationKeys.AccessKey, "key");
            _options.Value.Settings.Add(ConfigurationKeys.AccessToken, "token");
            _options.Value.Settings.Add(ConfigurationKeys.SecuredConnection, "false");
            _options.Value.Settings.Add(ConfigurationKeys.Region, "region");
        }

        [Fact]
        public async Task GivenAMinIoConnectionException_WhenCopyObjectIsCalled_ExpectExceptionToBeWrappedInStorageConnectionExceptionAsync()
        {
            await Assert.ThrowsAsync<StorageConnectionException>(async () =>
            {
                var service = new MinIoStorageService(_minIoClientFactory.Object, _amazonStsClient.Object, _options, _logger.Object);
                _objectOperations.Setup(p => p.CopyObjectAsync(It.IsAny<CopyObjectArgs>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ConnectionException("error", new ResponseResult(new HttpRequestMessage(), new Exception("inner exception"))));

                await service.CopyObjectAsync("sourceBucket", "sourceFile", "destinationBucket", "destinationFile");
            });
        }

        [Fact]
        public async Task GivenAnyException_WhenCopyObjectIsCalled_ExpectExceptionToBeWrappedInStorageServiceExceptionAsync()
        {
            await Assert.ThrowsAsync<StorageServiceException>(async () =>
            {
                var service = new MinIoStorageService(_minIoClientFactory.Object, _amazonStsClient.Object, _options, _logger.Object);
                _objectOperations.Setup(p => p.CopyObjectAsync(It.IsAny<CopyObjectArgs>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception("inner exception"));

                await service.CopyObjectAsync("sourceBucket", "sourceFile", "destinationBucket", "destinationFile");
            });
        }

        [Fact]
        public async Task GivenAListObjectCall_WhenCancellationIsRequested_ExpectTimeoutWithListObjectTimeoutException()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                var observable = Observable.Create<Item>(async (obs) =>
                {
                    while (true)
                    {
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            obs.OnError(new OperationCanceledException());
                            break;
                        }
                    }
                    return await Task.FromResult(Disposable.Empty);
                });
                var service = new MinIoStorageService(_minIoClientFactory.Object, _amazonStsClient.Object, _options, _logger.Object);
                _bucketOperations.Setup(p => p.ListObjectsAsync(It.IsAny<ListObjectsArgs>(), It.IsAny<CancellationToken>()))
                    .Returns(observable);

                cancellationTokenSource.CancelAfter(5000);
                await service.ListObjectsAsync("bucket", cancellationToken: cancellationTokenSource.Token);

            });
        }

        [Fact]
        public async Task GivenAListObjectCall_WhenErrorIsReceived_ExpectListObjectException()
        {
            var manualResetEvent = new ManualResetEvent(false);
            await Assert.ThrowsAsync<ListObjectException>(async () =>
            {
                var observable = Observable.Create<Item>(async (obs) =>
                {
                    obs.OnNext(new Item { Key = "key", ETag = "etag", Size = 1, IsDir = false });
                    obs.OnError(new Exception("error"));
                    obs.OnCompleted();
                    return await Task.FromResult(Disposable.Empty);
                });
                var service = new MinIoStorageService(_minIoClientFactory.Object, _amazonStsClient.Object, _options, _logger.Object);
                _bucketOperations.Setup(p => p.ListObjectsAsync(It.IsAny<ListObjectsArgs>(), It.IsAny<CancellationToken>()))
                    .Returns(observable);

                var listObjectTask = service.ListObjectsAsync("bucket");
                await Task.Delay(3000);

                await listObjectTask;
            });
        }

        [Fact]
        public async Task GivenAListObjectCall_WhenConnectionExceptionIsThrown_ExpectTheExceptionToBeWrappedInStorageConnectionException()
        {
            var manualResetEvent = new ManualResetEvent(false);
            await Assert.ThrowsAsync<StorageConnectionException>(async () =>
            {
                var service = new MinIoStorageService(_minIoClientFactory.Object, _amazonStsClient.Object, _options, _logger.Object);
                _bucketOperations.Setup(p => p.ListObjectsAsync(It.IsAny<ListObjectsArgs>(), It.IsAny<CancellationToken>()))
                    .Throws(new ConnectionException("error", new ResponseResult(new HttpRequestMessage(), new Exception("inner exception"))));

                await service.ListObjectsAsync("bucket");
            });
        }

        [Fact]
        public async Task GivenAListObjectCall_WhenAnyMinIoExceptionIsThrown_ExpectTheExceptionToBeWrappedInStorageServiceException()
        {
            var manualResetEvent = new ManualResetEvent(false);
            await Assert.ThrowsAsync<StorageServiceException>(async () =>
            {
                var service = new MinIoStorageService(_minIoClientFactory.Object, _amazonStsClient.Object, _options, _logger.Object);
                _bucketOperations.Setup(p => p.ListObjectsAsync(It.IsAny<ListObjectsArgs>(), It.IsAny<CancellationToken>()))
                    .Throws(new InvalidBucketNameException("bucket", "bad"));

                await service.ListObjectsAsync("bucket");
            });
        }
    }
}
