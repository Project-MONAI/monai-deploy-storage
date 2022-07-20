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

using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;
using Monai.Deploy.Storage.Minio.Extensions;
using Xunit;
using Moq;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Monai.Deploy.Storage.S3Policy.Policies;

namespace Monai.Deploy.Storage.MinIO.Tests
{
    public class MinioPolicyExtensionsTest
    {
        [Theory(DisplayName = "GetString ")]
        [InlineData(AccessPermissions.Diagnostics, "diagnostics")]
        [InlineData(AccessPermissions.ConsoleAdmin, "consoleAdmin")]
        [InlineData(AccessPermissions.Read, "readonly")]
        [InlineData(AccessPermissions.Write, "writeonly")]
        [InlineData(AccessPermissions.Read & AccessPermissions.Write, "readwrite")]
        public void GetString_Test(AccessPermissions permissions, string expectedValue)
        {
            Assert.Equal(expectedValue, permissions.GetString());
        }

        // integration test needs Minio setup
        //[Fact]
        public async Task Should_Set_Correct_Policy()
        {
            var optionSettings = new StorageServiceConfiguration
            {
                Settings = new Dictionary<string, string> {
                    { "executableLocation", "mc.exe" },
                    { "serviceName", "serviceName" },
                    { "endpoint", "localhost:9000" },
                    { "accessKey", "admin" },
                    { "accessToken", "password" },
                }
            };
            var options = Options.Create(optionSettings);

            var systemUnderTest = new StorageAdminService(options, new Mock<ILogger<MinIoStorageService>>().Object, new FileSystem());
            const string bucketName = "test-bucket";
            const string payloadId = "00000000-1000-0000-0000-000000000000";
            const string userName = "nameUsedForTests";

            var policys = new PolicyRequest[] { new PolicyRequest(bucketName, payloadId) };

            try
            {
                var result = await systemUnderTest.CreateUserAsync(userName, AccessPermissions.Write, policys);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }
            finally
            {
                await systemUnderTest.RemoveUserAsync(userName);
            }
        }
    }
}
