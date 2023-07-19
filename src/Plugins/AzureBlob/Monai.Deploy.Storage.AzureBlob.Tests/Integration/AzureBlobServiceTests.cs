using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Extensions.Ordering;

namespace Monai.Deploy.Storage.AzureBlob.Tests.Integration
{
    [Order(0), Collection("AzureBlobStorage")]
    public class AzureBlobServiceTests
    {
        private readonly Mock<ILogger<AzureBlobStorageService>> _logger;
        private readonly AzureBlobStorageFixture _fixture;
        private readonly AzureBlobStorageService _azureBlobService;
        private readonly string _testFileName;
        private readonly string _testFileNameCopy;

        public AzureBlobServiceTests(AzureBlobStorageFixture fixture)
        {
            _logger = new Mock<ILogger<AzureBlobStorageService>>();
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _azureBlobService = new AzureBlobStorageService(_fixture.ClientFactory, _fixture.Configurations, _logger.Object);
            _testFileName = $"Tao-Te-Ching/Laozi/chapter-one.zip";
            _testFileNameCopy = $"Tao-Te-Ching/Laozi/chapter-one=backup.zip";
        }


        [Fact, Order(1)]
        public async Task S01_GivenABucketToAzureBlob()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                //await _azureBlobService.CreateFolderAsync(_fixture.ContainerName, "");
                var containerClient = _fixture.ClientFactory.GetBlobContainerClient(_fixture.ContainerName);
                await containerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            Assert.Null(exception);
        }

        [Fact, Order(2)]
        public async Task S02_GivenASetOfDataAvailableToAzureBlob()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                await _fixture.GenerateAndUploadData().ConfigureAwait(false);
            }).ConfigureAwait(false);

            Assert.Null(exception);
        }

        [Theory, Order(3)]
        [InlineData(null, 4)]
        [InlineData("dir-1/", 1)]
        [InlineData("dir-2/", 2)]
        public async Task S03_WhenListObjectsAsyncIsCalled_ExpectItToListObjectsBasedOnParameters(string? prefix, int count)
        {
            var actual = await _azureBlobService.ListObjectsAsync(_fixture.ContainerName, prefix, true).ConfigureAwait(false);

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
            var actual = await _azureBlobService.VerifyObjectsExistAsync(_fixture.ContainerName, _fixture.Files).ConfigureAwait(false);

            actual.Should().NotBeEmpty()
                .And.HaveCount(_fixture.Files.Count);

            actual.Should().ContainValues(true);
        }

        [Fact, Order(5)]
        public async Task S05_GivenAFileUploadedToAzureBlob()
        {
            var data = _fixture.GetRandomBytes();
            var stream = new MemoryStream(data);
            await _azureBlobService.PutObjectAsync(_fixture.ContainerName, _testFileName, stream, data.Length, "application/binary", null).ConfigureAwait(false);

            var callback = (Stream stream) =>
            {
                var actual = new MemoryStream();
                stream.CopyTo(actual);
                actual.ToArray().Should().Equal(data);
            };
            var client = _fixture.ClientFactory.GetBlobClient(_fixture.ContainerName, _testFileName);

            var fileContentsStream = new MemoryStream();
            await client.DownloadToAsync(fileContentsStream).ConfigureAwait(false);
            fileContentsStream.ToArray().Should().Equal(data);

            var prop = await client.GetPropertiesAsync().ConfigureAwait(false);
            prop.Value.ContentLength.Should().Be(data.Length);
        }

        [Fact, Order(6)]
        public async Task S06_ExpectTheFileToBeBeDownloadable()
        {
            var stream = await _azureBlobService.GetObjectAsync(_fixture.ContainerName, _testFileName).ConfigureAwait(false);
            Assert.NotNull(stream);
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();

            var original = await DownloadData(_testFileName).ConfigureAwait(false);

            Assert.NotNull(original);

            data.Should().Equal(original);
        }

        [Fact, Order(7)]
        public async Task S07_GivenACopyOfTheFile()
        {
            await _azureBlobService.CopyObjectAsync(_fixture.ContainerName, _testFileName, _fixture.ContainerName, _testFileNameCopy).ConfigureAwait(false);

            var original = await DownloadData(_testFileName).ConfigureAwait(false);
            var copy = await DownloadData(_testFileNameCopy).ConfigureAwait(false);

            Assert.NotNull(original);
            Assert.NotNull(copy);

            copy.Should().Equal(original);
        }

        [Fact, Order(8)]
        public async Task S08_ExpectedBothOriginalAndCopiedToExist()
        {
            var files = new List<string>() { _testFileName, _testFileNameCopy, "file-does-not-exist" };
            var expectedResults = new List<bool>() { true, true, false };
            var results = await _azureBlobService.VerifyObjectsExistAsync(_fixture.ContainerName, files).ConfigureAwait(false);

            Assert.NotNull(results);

            results.Should().ContainKeys(files);
            results.Should().ContainValues(expectedResults);

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var result = await _azureBlobService.VerifyObjectExistsAsync(_fixture.ContainerName, file).ConfigureAwait(false);
                Assert.Equal(expectedResults[i], result);
            }
        }

        [Fact, Order(9)]
        public async Task S09_GivenADirectoryCreatedToAzureBlob()
        {
            var folderName = "my-folder";
            await _azureBlobService.CreateFolderAsync(_fixture.ContainerName, folderName).ConfigureAwait(false);
            var result = await _azureBlobService.VerifyObjectExistsAsync(_fixture.ContainerName, $"{folderName}/stubFile.txt").ConfigureAwait(false);

            Assert.True(result);
        }

        [Fact, Order(10)]
        public async Task S10_ExpectTheDirectoryToBeRemovable()
        {
            var folderName = "my - folder / stubFile.txt";
            await _azureBlobService.RemoveObjectAsync(_fixture.ContainerName, folderName).ConfigureAwait(false);
            var result = await _azureBlobService.VerifyObjectExistsAsync(_fixture.ContainerName, $"{folderName}/stubFile.txt").ConfigureAwait(false);
            Assert.False(result);

            var files = new List<string>() { _testFileName, _testFileNameCopy, "file-does-not-exist" };
            await _azureBlobService.RemoveObjectsAsync(_fixture.ContainerName, files).ConfigureAwait(false);
        }

        [Fact, Order(11)]
        public async Task S11_ExpectTheFilesToBeRemovable()
        {
            var files = new List<string>() { _testFileName, _testFileNameCopy, "file-does-not-exist" };
            await _azureBlobService.RemoveObjectsAsync(_fixture.ContainerName, files).ConfigureAwait(false);

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var result = await _azureBlobService.VerifyObjectExistsAsync(_fixture.ContainerName, file).ConfigureAwait(false);
                Assert.False(result);
            }
        }

        private async Task<byte[]> DownloadData(string filename)
        {
            var copiedStream = new MemoryStream();

            var client = _fixture.ClientFactory.GetBlobClient(_fixture.ContainerName, filename);
            await client.DownloadToAsync(copiedStream).ConfigureAwait(false);
            return copiedStream.ToArray();
        }
    }
}
