// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

namespace Monai.Deploy.Storage
{
    internal static class SR
    {
        public const string PlugInDirectoryName = "plug-ins";
        public static readonly string PlugInDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SR.PlugInDirectoryName);
    }
}
