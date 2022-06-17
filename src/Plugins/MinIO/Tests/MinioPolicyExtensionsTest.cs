// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Minio.Extensions;
using Xunit;

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
    }
}
