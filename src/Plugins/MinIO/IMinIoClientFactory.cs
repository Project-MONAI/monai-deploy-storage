/*
 * Copyright 2021-2024 MONAI Consortium
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Amazon.SecurityToken.Model;
using Minio;
using Minio.ApiEndpoints;

namespace Monai.Deploy.Storage.MinIO
{
    public interface IMinIoClientFactory
    {
        IMinioClient GetClient();

        IMinioClient GetClient(Credentials credentials);

        IMinioClient GetClient(Credentials credentials, string region);

        IObjectOperations GetObjectOperationsClient();

        IObjectOperations GetObjectOperationsClient(Credentials credentials);

        IObjectOperations GetObjectOperationsClient(Credentials credentials, string region);

        IBucketOperations GetBucketOperationsClient();

        IBucketOperations GetBucketOperationsClient(Credentials credentials);

        IBucketOperations GetBucketOperationsClient(Credentials credentials, string region);
    }
}
