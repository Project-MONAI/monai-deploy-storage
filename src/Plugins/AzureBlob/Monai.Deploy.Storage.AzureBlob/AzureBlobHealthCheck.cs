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

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Monai.Deploy.Storage.AzureBlob
{
    internal class AzureBlobHealthCheck : IHealthCheck
    {
        private readonly IAzureBlobClientFactory _azureBlobClientFactory;
        private readonly ILogger<AzureBlobHealthCheck> _logger;

        public AzureBlobHealthCheck(IAzureBlobClientFactory azureBlobClientFactory, ILogger<AzureBlobHealthCheck> logger)
        {
            _azureBlobClientFactory = azureBlobClientFactory ?? throw new ArgumentNullException(nameof(azureBlobClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
        {
            try
            {
                var client = _azureBlobClientFactory.GetBlobServiceClient();
                await client.GetBlobContainersAsync(cancellationToken: cancellationToken)
                .AsPages(pageSizeHint: 1)
                .GetAsyncEnumerator(cancellationToken)
                .MoveNextAsync()
                .ConfigureAwait(false); ;

                return HealthCheckResult.Healthy();
            }
            catch (Exception exception)
            {
                _logger.HealthCheckError(exception);
                return HealthCheckResult.Unhealthy(exception: exception);
            }
        }
    }
}
