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

using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Minio.Extensions;
using Xunit;

namespace Monai.Deploy.Storage.MinIO.Tests.Unit
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
    }
}
