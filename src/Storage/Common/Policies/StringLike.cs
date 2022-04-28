// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Newtonsoft.Json;

namespace Monai.Deploy.Storage.Common.Policies
{
    public class StringLike
    {
        [JsonProperty(PropertyName = "s3:prefix")]
        public string[] S3Prefix { get; set; }
    }
}
