using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.Configuration;
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.AzureBlob.Tests.Unit
{
    public class AzureBlobClientFactoryTests
    {
        private readonly Mock<ILogger<AzureBlobClientFactory>> _logger;

        public AzureBlobClientFactoryTests()
        {
            _logger = new Mock<ILogger<AzureBlobClientFactory>>();
        }

        [Fact]
        public void ShouldThrowOnNullOptions()
        {
            var factoryMock = new Mock<IAzureBlobClientFactory>();
            Assert.Throws<ArgumentNullException>(() => new AzureBlobClientFactory(null, _logger.Object));
        }

        [Fact]
        public void ShouldThrowOnNullLogger()
        {
            var factoryMock = new Mock<IAzureBlobClientFactory>();
            Assert.Throws<ArgumentNullException>(() => new AzureBlobClientFactory(Options.Create(new StorageServiceConfiguration()), null));
        }


        [Fact]
        public void ShouldThrowOnMisssingOptionsKey()
        {
            var factoryMock = new Mock<IAzureBlobClientFactory>();
            var options = Options.Create(new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { "endpoint", "somthing" } } });
            Assert.Throws<ConfigurationException>(() => new AzureBlobClientFactory(options, _logger.Object));
        }
    }
}
