// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Amazon.SecurityToken.Model;
using Minio;

namespace Monai.Deploy.Storage.MinIO
{
    public interface IMinIoClientFactory
    {
        MinioClient GetClient();

        MinioClient GetClient(Credentials credentials);

        MinioClient GetClient(Credentials credentials, string region);
    }
}
