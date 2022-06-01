// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

namespace Monai.Deploy.Storage.Common.Policies
{
    public class Statement
    {
        public string? Sid { get; set; }

        public string[]? Action { get; set; }

        public string? Effect { get; set; }

        public string[]? Resource { get; set; }

        public Condition? Condition { get; set; }
    }
}
