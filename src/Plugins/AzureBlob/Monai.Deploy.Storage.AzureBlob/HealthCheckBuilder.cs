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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
//using Monai.Deploy.Storage.MinIO;

namespace Monai.Deploy.Storage.AzureBlob
{
    public class HealthCheckBuilder : HealthCheckRegistrationBase
    {
        public override IHealthChecksBuilder ConfigureAdminHealthCheck(IHealthChecksBuilder builder, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, TimeSpan? timeout = null) => throw new NotImplementedException();
        public override IHealthChecksBuilder ConfigureHealthCheck(IHealthChecksBuilder builder, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, TimeSpan? timeout = null) =>
            builder.Add(new HealthCheckRegistration(
                ConfigurationKeys.StorageServiceName,
                serviceProvider =>
                {
                    var logger = serviceProvider.GetRequiredService<ILogger<AzureBlobHealthCheck>>();
                    var azureBlobClientFactory = serviceProvider.GetRequiredService<IAzureBlobClientFactory>();
                    return new AzureBlobHealthCheck(azureBlobClientFactory, logger);
                },
                failureStatus,
                tags,
                timeout));
    }
}
