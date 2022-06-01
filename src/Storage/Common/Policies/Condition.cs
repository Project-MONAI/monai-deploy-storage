// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

namespace Monai.Deploy.Storage.Common.Policies
{
    public class Condition
    {
        public StringLike? StringLike { get; set; }

        public StringEquals? StringEquals { get; set; }
    }
}
