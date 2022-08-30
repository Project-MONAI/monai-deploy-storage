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

namespace Monai.Deploy.Storage.MinIO
{
    internal class MinIoHealthCheck : IHealthCheck
    {
        private readonly IMinIoClientFactory _minIoClientFactory;
        private readonly ILogger<MinIoHealthCheck> _logger;

        public MinIoHealthCheck(IMinIoClientFactory minIoClientFactory, ILogger<MinIoHealthCheck> logger)
        {
            _minIoClientFactory = minIoClientFactory ?? throw new ArgumentNullException(nameof(minIoClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
        {
            try
            {
                var minioClient = _minIoClientFactory.GetClient();
                await minioClient.ListBucketsAsync(cancellationToken).ConfigureAwait(false);

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
