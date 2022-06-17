// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

namespace Monai.Deploy.Storage.AWSS3
{
    internal static class ConfigurationKeys
    {
        public static readonly string AccessKey = "accessKey";
        public static readonly string AccessToken = "accessToken";
        public static readonly string Region = "region";
        public static readonly string CredentialServiceUrl = "credentialServiceUrl";

        public static readonly string[] RequiredKeys = new[] { AccessKey, AccessToken, Region };
    }
}
