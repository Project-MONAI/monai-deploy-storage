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

using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.Configuration;
using Monai.Deploy.Storage.S3Policy.Policies;
using Moq;
using Xunit;
using Xunit.Extensions.Ordering;

namespace Monai.Deploy.Storage.MinIO.Tests.Integration
{
    [Order(1)]
    public class StorageAdminServiceTest
    {
        private readonly Mock<ILogger<StorageAdminService>> _logger;
        private readonly IOptions<StorageServiceConfiguration> _options;
        private readonly IFileSystem _fileSystem;
        private readonly StorageAdminService _minIoService;

        public StorageAdminServiceTest()
        {
            _logger = new Mock<ILogger<StorageAdminService>>();
            _options = Options.Create(new StorageServiceConfiguration());
            _fileSystem = new FileSystem();

            _options.Value.Settings.Add(ConfigurationKeys.EndPoint, "localhost:9000");
            _options.Value.Settings.Add(ConfigurationKeys.AccessKey, "minioadmin");
            _options.Value.Settings.Add(ConfigurationKeys.AccessToken, "minioadmin");
            _options.Value.Settings.Add(ConfigurationKeys.McServiceName, "MDTest");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _options.Value.Settings.Add(ConfigurationKeys.McExecutablePath, "./Integration/mc.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _options.Value.Settings.Add(ConfigurationKeys.McExecutablePath, "./Integration/mc");
            }
            else
            {
                throw new NotSupportedException("OS not supported");
            }

            _minIoService = new StorageAdminService(_options, _logger.Object, _fileSystem);
        }

        [Fact, Order(1)]
        public async Task S01_GivenAServiceConnection()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                var result = await _minIoService.SetConnectionAsync().ConfigureAwait(false);
                Assert.True(result);
            }).ConfigureAwait(false);

            Assert.Null(exception);
        }

        [Fact, Order(2)]
        public async Task S02_ExpectToHaveAConnection()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                var result = await _minIoService.HasConnectionAsync().ConfigureAwait(false);
                Assert.True(result);
            }).ConfigureAwait(false);

            Assert.Null(exception);
        }

        [Fact, Order(3)]
        public async Task S03_GivenAnUserWithPolicies()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                var policies = new PolicyRequest[] {
                    new PolicyRequest("my-bucket", "directory1"),
                    new PolicyRequest("my-bucket", "directory1/subdirectory2"),
                };
                await _minIoService.CreateUserAsync($"monai-deploy-{DateTime.UtcNow.Ticks}", policies).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Assert.Null(exception);
        }

        [Fact, Order(3)]
        public async Task S04_ExpectUserHasBeenCreated()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                var result = await _minIoService.UserAlreadyExistsAsync("monai-deploy").ConfigureAwait(false);
                Assert.True(result);
            }).ConfigureAwait(false);

            Assert.Null(exception);
        }
    }
}
