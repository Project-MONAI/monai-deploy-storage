using Ardalis.GuardClauses;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage.AzureBlob
{
    public class AzureBlobClientFactory : IAzureBlobClientFactory
    {
        private readonly ILogger _logger;
        private readonly IOptions<StorageServiceConfiguration> _options;
        private StorageServiceConfiguration Options { get; }

        private readonly BlobServiceClient _blobServiceClient;

        public AzureBlobClientFactory(IOptions<StorageServiceConfiguration> options, ILogger<AzureBlobClientFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            var configuration = options.Value;
            ValidateConfiguration(configuration);
            Options = configuration;

            _blobServiceClient = new BlobServiceClient(Options.Settings[ConfigurationKeys.ConnectionString]);
        }

        public BlobClient GetBlobClient(BlobContainerClient containerClient, string blob)
        {
            return containerClient.GetBlobClient(blob);
        }

        public BlobClient GetBlobClient(string containerName, string blob)
        {
            return GetBlobContainerClient(containerName).GetBlobClient(blob);
        }

        public BlockBlobClient GetBlobBlockClient(BlobContainerClient containerClient, string blob)
        {
            return containerClient.GetBlockBlobClient(blob);
        }

        public BlockBlobClient GetBlobBlockClient(string containerName, string blob)
        {
            return GetBlobContainerClient(containerName).GetBlockBlobClient(blob);
        }

        public BlobContainerClient GetBlobContainerClient(string containerName)
        {
            return _blobServiceClient.GetBlobContainerClient(containerName);
        }
        public BlobServiceClient GetBlobServiceClient()
        {
            return _blobServiceClient;
        }

        private void ValidateConfiguration(StorageServiceConfiguration configuration)
        {
            Guard.Against.Null(configuration, nameof(configuration));

            foreach (var key in ConfigurationKeys.RequiredKeys)
            {
                if (!configuration.Settings.ContainsKey(key))
                {
                    throw new ConfigurationException($"{nameof(AzureBlobClientFactory)} is missing configuration for {key}.");
                }
            }
        }
    }
}
