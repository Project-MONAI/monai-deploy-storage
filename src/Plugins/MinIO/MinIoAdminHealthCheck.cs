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

using System.Collections.ObjectModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Monai.Deploy.Storage.API;

namespace Monai.Deploy.Storage.MinIO
{
    internal class MinIoAdminHealthCheck : IHealthCheck
    {
        private readonly IStorageAdminService _storageAdminService;
        private readonly ILogger<MinIoAdminHealthCheck> _logger;

        public MinIoAdminHealthCheck(IStorageAdminService storageAdminService, ILogger<MinIoAdminHealthCheck> logger)
        {
            _storageAdminService = storageAdminService ?? throw new ArgumentNullException(nameof(storageAdminService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
        {
            try
            {
                var hasConnection = await _storageAdminService.HasConnectionAsync().ConfigureAwait(false);
                if (hasConnection is false)
                {
                    await _storageAdminService.SetConnectionAsync().ConfigureAwait(false);
                }

                var connectionResult = await _storageAdminService.GetConnectionAsync().ConfigureAwait(false);
                var joinedResult = string.Join("\n", connectionResult);

                var roDict = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>() { { "MinoAdminResult", joinedResult } });

                if (hasConnection)
                {
                    return HealthCheckResult.Healthy(data: roDict);
                }

                return HealthCheckResult.Unhealthy(data: roDict);
            }
            catch (Exception exception)
            {
                _logger.HealthCheckError(exception);
                return HealthCheckResult.Unhealthy(exception: exception);
            }
        }
    }
}
