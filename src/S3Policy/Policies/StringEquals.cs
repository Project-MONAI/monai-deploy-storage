// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Newtonsoft.Json;

namespace Monai.Deploy.Storage.S3Policy.Policies
{
    public class StringEquals
    {
        [JsonProperty(PropertyName = "s3:prefix")]
        public string[]? S3Prefix { get; set; }

        [JsonProperty(PropertyName = "s3:delimiter")]
        public string[]? S3Delimiter { get; set; }
    }
}
