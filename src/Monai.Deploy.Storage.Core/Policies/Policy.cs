// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Newtonsoft.Json;

namespace Monai.Deploy.Storage.Core.Policies
{
    public partial class Policy
    {
        public string Version { get; set; } = "2012-10-17";

        public IList<Statement> Statement { get; set; } = new List<Statement>();

        public static Policy? FromJson(string json) => JsonConvert.DeserializeObject<Policy>(json, Converter.Settings);
    }
}
