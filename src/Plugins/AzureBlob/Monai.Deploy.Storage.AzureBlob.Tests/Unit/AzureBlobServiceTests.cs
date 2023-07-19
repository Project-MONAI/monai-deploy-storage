using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.Configuration;
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.AzureBlob.Tests.Unit
{
    public class AzureBlobServiceTests
    {
        private readonly Mock<ILogger<AzureBlobHealthCheck>> _logger;

        public AzureBlobServiceTests()
        {
            _logger = new Mock<ILogger<AzureBlobHealthCheck>>();
        }

        [Fact]
        public async Task ShouldThrowOnNullOptions()
        {
            var factoryMock = new Mock<IAzureBlobClientFactory>();
            Assert.Throws<ArgumentNullException>(() => new AzureBlobStorageService(factoryMock.Object, null, new Mock<ILogger<AzureBlobStorageService>>().Object));
        }

        [Fact]
        public async Task ShouldThrowOnNullFactory()
        {
            var options = Options.Create(new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { "endpoint", "somthing" } } });
            Assert.Throws<ArgumentNullException>(() => new AzureBlobStorageService(null, options, new Mock<ILogger<AzureBlobStorageService>>().Object));
        }

        [Fact]
        public async Task ShouldThrowOnNullLogger()
        {
            var options = Options.Create(new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { "endpoint", "somthing" } } });
            var factoryMock = new Mock<IAzureBlobClientFactory>();
            Assert.Throws<ArgumentNullException>(() => new AzureBlobStorageService(factoryMock.Object, options, null));
        }

        [Fact]
        public async Task ShouldThrowOnMissingOptionsKey()
        {
            var options = Options.Create(new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { "endpoint", "somthing" } } });
            var factoryMock = new Mock<IAzureBlobClientFactory>();
            Assert.Throws<ConfigurationException>(() => new AzureBlobStorageService(factoryMock.Object, options, new Mock<ILogger<AzureBlobStorageService>>().Object));
        }

        [Fact]
        public async Task ShouldListBlobs()
        {
            var ops = new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { "azureBlobConnectionString", "UseDevelopmentStorage=true" } } };
            var options = Options.Create(ops);
            var factory = new AzureBlobClientFactory(options, new Mock<ILogger<AzureBlobClientFactory>>().Object);
            var service = new AzureBlobStorageService(factory, options, new Mock<ILogger<AzureBlobStorageService>>().Object);

            var result = await service.ListObjectsAsync("basecontainer", "Aw", false);
            Assert.Single(result);

            result = await service.ListObjectsAsync("basecontainer", "no", true);
            Assert.Equal(2, result.Count);

            var stream = await service.GetObjectAsync("basecontainer", "other2/noretain.json");
            //await service.CopyObjectAsync("basecontainer/other", "folder/noretain.json", "basecontainer", "other2/noretain.json");
            var exists = await service.VerifyObjectExistsAsync("basecontainer/other2", "noretain.json");
            var exists2 = await service.VerifyObjectsExistAsync("basecontainer", new List<string> { "other2/noretain.json", "other/noretain.json" });

            var localFilePath = "C:\\Users\\NeilSouth\\source\\repos\\monai-deploy-storage\\src\\Plugins\\AzureBlob\\Monai.Deploy.Storage.AzureBlob.Tests\\Unit\\AzureBlobServiceTests.cs";
            var fileStream = File.OpenRead(localFilePath);
            await service.PutObjectAsync(
                "basecontainer/other",
                "newFile.cs",
                fileStream,
                fileStream.Length,
                "text/plain",
                new Dictionary<string, string> { { "author", "neil" } });

            //await service.RemoveObjectAsync("basecontainer", "newFile.cs");

            await service.RemoveObjectsAsync("basecontainer", new List<string> { "other/newFile.cs", "other/folder/noretain.json" });

            await service.CreateFolderAsync("basecontainer", "my/new/folder");

        }
    }
}
