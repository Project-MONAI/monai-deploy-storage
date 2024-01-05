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

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Minio.DataModel.Args;
using Moq;
using Xunit;
using Xunit.Extensions.Ordering;

namespace Monai.Deploy.Storage.MinIO.Tests.Integration
{
    [Order(0), Collection("MinIoStorage")]
    public class MinIoStorageServiceTest
    {
        private readonly MinIoStorageFixture _fixture;
        private readonly Mock<IAmazonSecurityTokenServiceClientFactory> _amazonStsClient;
        private readonly Mock<ILogger<MinIoStorageService>> _logger;
        private readonly MinIoStorageService _minIoService;
        private readonly string _testFileName;
        private readonly string _testFileNameCopy;

        public MinIoStorageServiceTest(MinIoStorageFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _amazonStsClient = new Mock<IAmazonSecurityTokenServiceClientFactory>();
            _logger = new Mock<ILogger<MinIoStorageService>>();
            _minIoService = new MinIoStorageService(_fixture.ClientFactory, _amazonStsClient.Object, _fixture.Configurations, _logger.Object);
            _testFileName = $"Tao-Te-Ching/Laozi/chapter-one.zip";
            _testFileNameCopy = $"Tao-Te-Ching/Laozi/chapter-one=backup.zip";
        }

        [Fact, Order(1)]
        public async Task S01_GivenABucketOnMinIo()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                var client = _fixture.ClientFactory.GetBucketOperationsClient();
                var makeBucketArgs = new MakeBucketArgs()
                        .WithBucket(_fixture.BucketName)
                        .WithLocation(_fixture.Location);
                await client.MakeBucketAsync(makeBucketArgs);
            });

            Assert.Null(exception);
        }

        [Fact, Order(2)]
        public async Task S02_GivenASetOfDataAvailableOnMinIo()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                var client = _fixture.ClientFactory.GetObjectOperationsClient();
                await _fixture.GenerateAndUploadData(client);
            });

            Assert.Null(exception);
        }

        [Theory, Order(3)]
        [InlineData(null, 4)]
        [InlineData("dir-1/", 1)]
        [InlineData("dir-2/", 2)]
        public async Task S03_WhenListObjectsAsyncIsCalled_ExpectItToListObjectsBasedOnParameters(string? prefix, int count)
        {
            var actual = await _minIoService.ListObjectsAsync(_fixture.BucketName, prefix, true);

            actual.Should().NotBeEmpty()
                .And.HaveCount(count);

            var expected = _fixture.Files.ToList();
            if (prefix is not null)
            {
                expected = expected.Where(p => p.StartsWith(prefix)).ToList();
            }
            actual.Select(p => p.FilePath).Should().BeEquivalentTo(expected);
        }

        [Fact, Order(4)]
        public async Task S04_WhenVerifyObjectsExistAsyncIsCalled_ExpectToReturnAll()
        {
            var actual = await _minIoService.VerifyObjectsExistAsync(_fixture.BucketName, _fixture.Files);

            actual.Should().NotBeEmpty()
                .And.HaveCount(_fixture.Files.Count);

            actual.Should().ContainValues(true);
        }

        [Fact, Order(5)]
        public async Task S05_GivenAFileUploadedOnMinIo()
        {
            var data = _fixture.GetRandomBytes();
            var stream = new MemoryStream(data);
            await _minIoService.PutObjectAsync(_fixture.BucketName, _testFileName, stream, data.Length, "application/binary", null);

            var callback = (Stream stream) =>
            {
                var actual = new MemoryStream();
                stream.CopyTo(actual);
                actual.ToArray().Should().Equal(data);
            };
            var client = _fixture.ClientFactory.GetObjectOperationsClient();
            var args = new GetObjectArgs()
                    .WithBucket(_fixture.BucketName)
                    .WithObject(_testFileName)
                    .WithCallbackStream(callback);
            var obj = await client.GetObjectAsync(args);
            obj.Should().NotBeNull();
            obj.Size.Should().Be(data.Length);
        }

        [Fact, Order(6)]
        public async Task S06_ExpectTheFileToBeBeDownloadable()
        {
            var stream = await _minIoService.GetObjectAsync(_fixture.BucketName, _testFileName);
            Assert.NotNull(stream);
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();

            var original = await DownloadData(_testFileName);

            Assert.NotNull(original);

            data.Should().Equal(original);
        }

        [Fact, Order(7)]
        public async Task S07_GivenACopyOfTheFile()
        {
            await _minIoService.CopyObjectAsync(_fixture.BucketName, _testFileName, _fixture.BucketName, _testFileNameCopy);

            var original = await DownloadData(_testFileName);
            var copy = await DownloadData(_testFileNameCopy);

            Assert.NotNull(original);
            Assert.NotNull(copy);

            copy.Should().Equal(original);
        }

        [Fact, Order(8)]
        public async Task S08_ExpectedBothOriginalAndCopiedToExist()
        {
            var files = new List<string>() { _testFileName, _testFileNameCopy, "file-does-not-exist" };
            var expectedResults = new List<bool>() { true, true, false };
            var results = await _minIoService.VerifyObjectsExistAsync(_fixture.BucketName, files);

            Assert.NotNull(results);

            results.Should().ContainKeys(files);
            results.Should().ContainValues(expectedResults);

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var result = await _minIoService.VerifyObjectExistsAsync(_fixture.BucketName, file);
                Assert.Equal(expectedResults[i], result);
            }
        }

        [Fact, Order(9)]
        public async Task S09_GivenADirectoryCreatedOnMinIo()
        {
            var folderName = "my-folder";
            await _minIoService.CreateFolderAsync(_fixture.BucketName, folderName);
            var result = await _minIoService.VerifyObjectExistsAsync(_fixture.BucketName, $"{folderName}/stubFile.txt");

            Assert.True(result);
        }

        [Fact, Order(10)]
        public async Task S10_ExpectTheDirectoryToBeRemovable()
        {
            var folderName = "my - folder / stubFile.txt";
            await _minIoService.RemoveObjectAsync(_fixture.BucketName, folderName);
            var result = await _minIoService.VerifyObjectExistsAsync(_fixture.BucketName, $"{folderName}/stubFile.txt");
            Assert.False(result);

            var files = new List<string>() { _testFileName, _testFileNameCopy, "file-does-not-exist" };
            await _minIoService.RemoveObjectsAsync(_fixture.BucketName, files);
        }

        [Fact, Order(11)]
        public async Task S11_ExpectTheFilesToBeRemovable()
        {
            var files = new List<string>() { _testFileName, _testFileNameCopy, "file-does-not-exist" };
            await _minIoService.RemoveObjectsAsync(_fixture.BucketName, files);

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var result = await _minIoService.VerifyObjectExistsAsync(_fixture.BucketName, file);
                Assert.False(result);
            }
        }

        private async Task<byte[]> DownloadData(string filename)
        {
            var copiedStream = new MemoryStream();
            var manulReset = new ManualResetEventSlim();
            var callback = (Stream stream) =>
            {
                stream.CopyTo(copiedStream);
                copiedStream.Position = 0;
                manulReset.Set();
            };

            var client = _fixture.ClientFactory.GetObjectOperationsClient();
            var copiedArgs = new GetObjectArgs()
                    .WithBucket(_fixture.BucketName)
                    .WithObject(filename)
                    .WithCallbackStream(callback);
            var copiedObject = await client.GetObjectAsync(copiedArgs);
            copiedObject.Should().NotBeNull();
            manulReset.Wait();
            return copiedStream.ToArray();
        }
    }
}
