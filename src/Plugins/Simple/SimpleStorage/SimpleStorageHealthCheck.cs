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
using Monai.Deploy.Storage.API;

namespace Monai.Deploy.Storage.SimpleStorage
{
    internal class SimpleStorageHealthCheck : IHealthCheck
    {
        private readonly ILogger<SimpleStorageHealthCheck> _logger;
        private readonly IStorageService _simpleStorageService;

        public SimpleStorageHealthCheck(IStorageService simpleStorageService, ILogger<SimpleStorageHealthCheck> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _simpleStorageService = simpleStorageService ?? throw new ArgumentNullException(nameof(simpleStorageService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
        {
            try
            {
                await _simpleStorageService.ListObjectsAsync("", cancellationToken: cancellationToken).ConfigureAwait(false);

                return HealthCheckResult.Healthy();
            }
            catch (Exception exception)
            {
                _logger.HealthCheckError(exception, exception.Message);
                return HealthCheckResult.Unhealthy(exception: exception);
            }
        }
    }
}
