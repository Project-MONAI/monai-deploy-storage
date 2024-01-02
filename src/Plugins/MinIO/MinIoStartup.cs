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

using Ardalis.GuardClauses;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio.ApiEndpoints;
using Minio.DataModel.Args;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage.MinIO
{
    public class MinIoStartup : IHostedService
    {
        private readonly IMinIoClientFactory _minIoClientFactory;
        private readonly IOptions<StorageServiceConfiguration> _options;
        private readonly ILogger<MinIoStartup> _logger;

        public MinIoStartup(
            IMinIoClientFactory minIoClientFactory,
            IOptions<StorageServiceConfiguration> options,
            ILogger<MinIoStartup> logger)
        {
            _minIoClientFactory = minIoClientFactory ?? throw new ArgumentNullException(nameof(minIoClientFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_options.Value.Settings.ContainsKey(ConfigurationKeys.CreateBuckets))
            {
                var buckets = _options.Value.Settings[ConfigurationKeys.CreateBuckets];
                var region = _options.Value.Settings.ContainsKey(ConfigurationKeys.Region) ? _options.Value.Settings[ConfigurationKeys.Region] : string.Empty;

                if (!string.IsNullOrWhiteSpace(buckets))
                {
                    var exceptions = new List<Exception>();
                    var bucketNames = buckets.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    var client = _minIoClientFactory.GetBucketOperationsClient();

                    foreach (var bucket in bucketNames)
                    {
                        try
                        {
                            await CreateBucket(client, bucket.Trim(), region, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.ErrorCreatingBucket(bucket, region, ex);
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
                _logger.NoBucketCreated();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task CreateBucket(IBucketOperations client, string bucket, string region, CancellationToken cancellationToken)
        {
            Guard.Against.Null(client, nameof(client));
            Guard.Against.Null(bucket, nameof(bucket));

            var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucket);
            if (!await client.BucketExistsAsync(bucketExistsArgs, cancellationToken).ConfigureAwait(false))
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(bucket);
                if (!string.IsNullOrWhiteSpace(region))
                {
                    makeBucketArgs.WithLocation(region);
                }
                await client.MakeBucketAsync(makeBucketArgs, cancellationToken).ConfigureAwait(false);
                _logger.BucketCreated(bucket, region);
            }
        }
    }
}
