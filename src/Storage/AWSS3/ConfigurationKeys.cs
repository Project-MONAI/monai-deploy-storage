// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

namespace Monai.Deploy.AWSS3
{
    internal static class ConfigurationKeys
    {
        public static readonly string EndPoint = "endpoint";
        public static readonly string AccessKey = "accessKey";
        public static readonly string AccessToken = "accessToken";
        public static readonly string Region = "region";
        public static readonly string CredentialServiceUrl = "credentialServiceUrl";

        public static readonly string[] RequiredKeys = new[] { EndPoint, AccessKey, AccessToken, Region };
    }
}
