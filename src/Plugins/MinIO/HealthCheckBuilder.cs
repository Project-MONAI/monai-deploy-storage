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

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Monai.Deploy.Storage.MinIO
{
    public class HealthCheckBuilder : HealthCheckRegistrationBase
    {
        public HealthCheckBuilder(string fullyQualifiedAssemblyName) : base(fullyQualifiedAssemblyName)
        {
        }

        public override IHealthChecksBuilder Configure(
            IHealthChecksBuilder builder,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
        {

            builder.Add(new HealthCheckRegistration(
                ConfigurationKeys.StorageServiceName,
                serviceProvider =>
                {
                    var minioClientFactory = serviceProvider.GetRequiredService<IMinIoClientFactory>();
                    var logger = serviceProvider.GetRequiredService<ILogger<MinIoHealthCheck>>();
                    return new MinIoHealthCheck(minioClientFactory, logger);
                },
                failureStatus,
                tags,
                timeout));
            return builder;
        }
    }
}
