/*
 * Copyright 2022-2023 MONAI Consortium
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
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.AzureBlob.Tests
{
    public class AzureBlobHealthCheckTest
    {
        private readonly Mock<IAzureBlobClientFactory> _azureblobClientFactory;
        private readonly Mock<ILogger<AzureBlobHealthCheck>> _logger;

        public AzureBlobHealthCheckTest()
        {
            _azureblobClientFactory = new Mock<IAzureBlobClientFactory>();
            _logger = new Mock<ILogger<AzureBlobHealthCheck>>();
        }

        [Fact]
        public async Task CheckHealthAsync_WhenFailedToListBucket_ReturnUnhealthy()
        {
            _azureblobClientFactory.Setup(p => p.GetBlobContainerClient(It.IsAny<string>())).Throws(new Exception("error"));

            var healthCheck = new AzureBlobHealthCheck(_azureblobClientFactory.Object, _logger.Object);
            var results = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(false);

            Assert.Equal(HealthStatus.Unhealthy, results.Status);
            Assert.NotNull(results.Exception);
            Assert.Equal("error", results.Exception.Message);
        }
    }
}
