/*
 * Copyright 2021-2022 MONAI Consortium
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Monai.Deploy.Storage.S3Policy.Policies;
using Newtonsoft.Json;

namespace Monai.Deploy.Storage.S3Policy.Tests.Extensions
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

        [Fact]
        public void GetPathList_NullFolder_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => PolicyExtensions.GetPathList(null));
        }

        #endregion GetPathList

        #region ToPolicy

        [Fact]
        public void ToPolicy_ValidBucketAndFolder()
        {
            var policy = PolicyExtensions.ToPolicy("test-bucket", "Jack/Is/The/Best");

            var policyString = JsonConvert.SerializeObject(policy, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Assert.Equal("{\"Version\":\"2012-10-17\",\"Statement\":[{\"Sid\":\"AllowUserToSeeBucketListInTheConsole\",\"Action\":[\"s3:ListAllMyBuckets\",\"s3:GetBucketLocation\"],\"Effect\":\"Allow\",\"Resource\":[\"arn:aws:s3:::*\"]},{\"Sid\":\"AllowRootAndHomeListingOfBucket\",\"Action\":[\"s3:ListBucket\"],\"Effect\":\"Allow\",\"Resource\":[\"arn:aws:s3:::test-bucket\"],\"Condition\":{\"StringEquals\":{\"s3:prefix\":[\"Jack/Is/The/Best\",\"Jack/Is/The/\",\"Jack/Is/\",\"Jack/\",\"\"],\"s3:delimiter\":[\"/\"]}}},{\"Sid\":\"AllowListingOfUserFolder\",\"Action\":[\"s3:ListBucket\"],\"Effect\":\"Allow\",\"Resource\":[\"arn:aws:s3:::test-bucket\"],\"Condition\":{\"StringEquals\":{\"s3:prefix\":[\"Jack/Is/The/Best/*\"]}}},{\"Sid\":\"AllowAllS3ActionsInUserFolder\",\"Action\":[\"s3:*\"],\"Effect\":\"Allow\",\"Resource\":[\"arn:aws:s3:::test-bucket/Jack/Is/The/Best\",\"arn:aws:s3:::test-bucket/Jack/Is/The/Best/*\"]}]}", policyString);
        }

        [Fact]
        public void ToPolicy_NullBucket_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => PolicyExtensions.ToPolicy(null, "Jack/Is/The/Best"));
        }

        [Fact]
        public void ToPolicy_NullFolder_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => PolicyExtensions.ToPolicy("test-bucket", null));
        }

        [Fact]
        public void ToPolicy_Should_Set_Correct_Allow_All_Path()
        {
            const string bucketName = "test-bucket";
            const string payloadId = "00000000-1000-0000-0000-000000000000";

            var policys = new PolicyRequest[] { new PolicyRequest(bucketName, payloadId) };

            var policyMade = PolicyExtensions.ToPolicy(policys);


            Assert.Collection(policyMade.Statement.First(p => p.Sid == "AllowAllS3ActionsInUserFolder").Resource!,
                (item) => item.Equals($"{bucketName}/{payloadId}"),
                (item) => item.Equals($"{bucketName}/{payloadId}/*"));

        }

        #endregion ToPolicy
    }
}
