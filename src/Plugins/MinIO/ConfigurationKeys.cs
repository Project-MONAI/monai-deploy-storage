/*
 * Copyright 2021-2025 MONAI Consortium
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

namespace Monai.Deploy.Storage.MinIO
{
    internal static class ConfigurationKeys
    {
        public static readonly string StorageServiceName = "minio";

        public static readonly string EndPoint = "endpoint";
        public static readonly string AccessKey = "accessKey";
        public static readonly string AccessToken = "accessToken";
        public static readonly string SecuredConnection = "securedConnection";
        public static readonly string Region = "region";
        public static readonly string CredentialServiceUrl = "credentialServiceUrl";
        public static readonly string McExecutablePath = "executableLocation";
        public static readonly string McServiceName = "serviceName";
        public static readonly string CreateBuckets = "createBuckets";
        public static readonly string ApiCallTimeout = "timeout";

        public static readonly string[] RequiredKeys = new[] { EndPoint, AccessKey, AccessToken, SecuredConnection, Region };
        public static readonly string[] McRequiredKeys = new[] { EndPoint, AccessKey, AccessToken, McExecutablePath, McServiceName };
    }
}
