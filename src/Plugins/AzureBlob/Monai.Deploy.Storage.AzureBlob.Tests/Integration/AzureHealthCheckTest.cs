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
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.AzureBlob;
using Monai.Deploy.Storage.Configuration;
using Moq;
using Xunit;
using Xunit.Extensions.Ordering;

namespace Monai.Deploy.Storage.AzureBlob.Tests.Integration
{
    [Order(10)]
    public class AzureHealthCheckTest
    {
        private readonly AzureBlobClientFactory _azureBlobClientFactory;
        private readonly Mock<ILogger<AzureBlobHealthCheck>> _logger = new Mock<ILogger<AzureBlobHealthCheck>>();

        public AzureHealthCheckTest()
        {
            var ops = new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { "azureBlobConnectionString", "UseDevelopmentStorage=true" } } };
            var options = Options.Create(ops);
            _azureBlobClientFactory = new AzureBlobClientFactory(options, new Mock<ILogger<AzureBlobClientFactory>>().Object);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenListBucketSucceeds_ReturnHealthy()
        {
            var healthCheck = new AzureBlobHealthCheck(_azureBlobClientFactory, new Mock<ILogger<AzureBlobHealthCheck>>().Object);
            var results = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(false);

            Assert.Equal(HealthStatus.Healthy, results.Status);
            Assert.Null(results.Exception);
        }
    }
}
