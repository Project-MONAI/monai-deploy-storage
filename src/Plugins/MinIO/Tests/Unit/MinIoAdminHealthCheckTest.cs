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
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.MinIO.Tests.Unit
{
    public class MinIoAdminHealthCheckTest
    {
        private readonly Mock<IStorageAdminService> _storageAdminService;
        private readonly Mock<ILogger<MinIoAdminHealthCheck>> _logger;

        public MinIoAdminHealthCheckTest()
        {
            _storageAdminService = new Mock<IStorageAdminService>();
            _logger = new Mock<ILogger<MinIoAdminHealthCheck>>();
        }

        [Fact]
        public async Task CheckHealthAsync_WhenConnectionThrows_ReturnUnhealthy()
        {
            _storageAdminService.Setup(p => p.HasConnectionAsync()).Throws(new Exception("error"));

            var healthCheck = new MinIoAdminHealthCheck(_storageAdminService.Object, _logger.Object);
            var results = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, results.Status);
            Assert.NotNull(results.Exception);
            Assert.Equal("error", results.Exception.Message);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenConnectionSucceeds_ReturnHealthy()
        {
            _storageAdminService.Setup(p => p.HasConnectionAsync()).ReturnsAsync(true);
            _storageAdminService.Setup(p => p.GetConnectionAsync()).ReturnsAsync(new List<string>() { "strings" });
            var healthCheck = new MinIoAdminHealthCheck(_storageAdminService.Object, _logger.Object);
            var results = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, results.Status);
            Assert.Null(results.Exception);
        }
    }
}
