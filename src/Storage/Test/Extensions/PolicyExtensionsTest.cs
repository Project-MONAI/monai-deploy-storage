// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System.Collections.Generic;
using Monai.Deploy.Storage.Common.Extensions;
using Xunit;

namespace Monai.Deploy.Storage.Test.Extensions
{
    public class PolicyExtensionsTest
    {
        #region GetPathList

        [Fact]
        public void GetPathList_MultiLevelLongPathReturnsValidList()
        {
            var actualList = PolicyExtensions.GetPathList("Jack/Is/The/Best");

            var expectedList = new List<string>
            {
                "Jack/Is/The/Best",
                "Jack/Is/The/",
                "Jack/Is/",
                "Jack/",
                ""
            };

            Assert.Equal(expectedList, actualList);
        }

        [Fact]
        public void GetPathList_MultiLevelShortPathReturnsValidList()
        {
            var actualList = PolicyExtensions.GetPathList("Home/Jack");

            var expectedList = new List<string>
            {
                "Home/Jack",
                "Home/",
                ""
            };

            Assert.Equal(expectedList, actualList);
        }

        [Fact]
        public void GetPathList_SingleLevelPathReturnsValidList()
        {
            var actualList = PolicyExtensions.GetPathList("Home");

            var expectedList = new List<string>
            {
                "Home",
                ""
            };

            Assert.Equal(expectedList, actualList);
        }

        #endregion
    }
}
