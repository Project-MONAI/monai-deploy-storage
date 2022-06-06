// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System;
using System.Collections.Generic;
using System.Text;
using Minio;

namespace Monai.Deploy.Storage.MinIo
{
    public interface IMinIoClientFactory
    {
        MinioClient GetClient();
    }
}
