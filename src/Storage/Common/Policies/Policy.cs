// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

namespace Monai.Deploy.Storage.Common.Policies
{
    public class Policy
    {
        public string Version { get; set; } = "2012-10-17";

        public IList<Statement> Statement { get; set; } = new List<Statement>();
    }
}
