// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

namespace Monai.Deploy.Storage.MinIO.MinIoAdmin.Models
{
    public enum MinioPolicy
    {
        ReadOnly,
        ConsoleAdmin,
        Diagnostics,
        Readwrite,
        WriteOnly,
    }
}
