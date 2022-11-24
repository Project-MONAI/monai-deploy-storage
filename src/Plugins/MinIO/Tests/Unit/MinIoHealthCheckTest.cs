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
using Minio;
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.MinIO.Tests.Unit
{
    public class MinIoHealthCheckTest
    {
        private readonly Mock<IMinIoClientFactory> _minIoClientFactory;
        private readonly Mock<ILogger<MinIoHealthCheck>> _logger;

        public MinIoHealthCheckTest()
        {
            _minIoClientFactory = new Mock<IMinIoClientFactory>();
            _logger = new Mock<ILogger<MinIoHealthCheck>>();
        }

        [Fact]
        public async Task CheckHealthAsync_WhenFailedToListBucket_ReturnUnhealthy()
        {
            _minIoClientFactory.Setup(p => p.GetBucketOperationsClient()).Throws(new Exception("error"));

            var healthCheck = new MinIoHealthCheck(_minIoClientFactory.Object, _logger.Object);
            var results = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(false);

            Assert.Equal(HealthStatus.Unhealthy, results.Status);
            Assert.NotNull(results.Exception);
            Assert.Equal("error", results.Exception.Message);
        }
    }
}
