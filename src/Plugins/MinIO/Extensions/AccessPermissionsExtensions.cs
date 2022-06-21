// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Monai.Deploy.Storage.API;

namespace Monai.Deploy.Storage.Minio.Extensions
{
    public static class AccessPermissionsExtensions
    {
        public static string GetString(this AccessPermissions policy) => policy switch
        {
            (AccessPermissions.Read & AccessPermissions.Write) => "readwrite",
            AccessPermissions.ConsoleAdmin => "consoleAdmin",
            AccessPermissions.Diagnostics => "diagnostics",
            AccessPermissions.Read => "readonly",
            AccessPermissions.Write => "writeonly",
            _ => "",
        };
    }
}
