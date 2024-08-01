using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.Configuration;
using Azure.Storage.Blobs;
using Azure.Identity;

namespace Monai.Deploy.Storage.AzureBlob
{
    public class AzureBlobStartup : IHostedService
    {
        private readonly Microsoft.Extensions.Options.IOptions<StorageServiceConfiguration> _options;
        private readonly ILogger<AzureBlobStartup> _logger;

        public AzureBlobStartup(IOptions<StorageServiceConfiguration> options, ILogger<AzureBlobStartup> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_options.Value.Settings.ContainsKey(ConfigurationKeys.CreateBuckets))
            {
                var buckets = _options.Value.Settings[ConfigurationKeys.CreateBuckets];

                if (!string.IsNullOrWhiteSpace(buckets))
                {
                    var exceptions = new List<Exception>();
                    var bucketNames = buckets.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    var blobServiceClient = new BlobServiceClient(_options.Value.Settings[ConfigurationKeys.ConnectionString]);


                    foreach (var bucket in bucketNames)
                    {
                        try
                        {
                            await blobServiceClient.CreateBlobContainerAsync(bucket.Trim(), cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.ErrorCreatingContainer(bucket, ex);
                            exceptions.Add(ex);
                        }
                    }

                    if (exceptions.Any())
                    {
                        throw new AggregateException("Error creating buckets.", exceptions);
                    }
                }
            }
            else
            {
                _logger.NoContainerCreated();
            }
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
