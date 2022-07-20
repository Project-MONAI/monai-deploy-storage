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

using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Monai.Deploy.Storage.S3Policy.Policies;
using Newtonsoft.Json;
using static Monai.Deploy.Storage.S3Policy.Policy;

namespace Monai.Deploy.Storage.S3Policy
{
    public static class PolicyExtensions
    {
        public static Policy ToPolicy(string? bucketName, string? folderName)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            Guard.Against.NullOrWhiteSpace(folderName, nameof(folderName));

            var pathList = GetPathList(folderName);

            return new Policy
            {
                Statement = new List<Statement>
                {
                    new Statement
                    {
                        Sid = "AllowUserToSeeBucketListInTheConsole",
                        Action = new string[] {"s3:ListAllMyBuckets", "s3:GetBucketLocation" },
                        Effect = "Allow",
                        Resource = new string[] { "arn:aws:s3:::*" }
                    },
                    new Statement
                    {
                        Sid = "AllowRootAndHomeListingOfBucket",
                        Action = new string[] { "s3:ListBucket" },
                        Effect = "Allow",
                        Resource = new string[] { $"arn:aws:s3:::{bucketName}" },
                        Condition = new Condition
                        {
                            StringEquals = new StringEquals
                            {
                                S3Prefix = pathList.ToArray(),
                                S3Delimiter = new string[] { "/" }
                            }
                        }
                    },
                    new Statement
                    {
                        Sid = "AllowListingOfUserFolder",
                        Action = new string[] { "s3:ListBucket" },
                        Effect = "Allow",
                        Resource = new string[] { $"arn:aws:s3:::{bucketName}" },
                        Condition = new Condition
                        {
                            StringEquals = new StringEquals
                            {
                                S3Prefix = new string[] {$"{folderName}/*" }
                            }
                        }
                    },
                    new Statement
                    {
                        Sid = "AllowAllS3ActionsInUserFolder",
                        Action = new string[] { "s3:*" },
                        Effect = "Allow",
                        Resource = new string[] { $"arn:aws:s3:::{bucketName}/{folderName}/*" },
                    },
                }
            };
        }

        public static Policy ToPolicy(PolicyRequest[] policyRequests)
        {
            Guard.Against.NullOrEmpty(policyRequests, nameof(policyRequests));

            var pathList = policyRequests.SelectMany(pr => GetPathList(pr.FolderName));
            Guard.Against.NullOrEmpty(pathList, nameof(pathList));

            return new Policy
            {
                Statement = new List<Statement>
                {
                    new Statement
                    {
                        Sid = "AllowUserToSeeBucketListInTheConsole",
                        Action = new string[] {"s3:ListAllMyBuckets", "s3:GetBucketLocation" },
                        Effect = "Allow",
                        Resource = policyRequests.Select(pr => pr.BucketName).ToArray(),
                    },
                    new Statement
                    {
                        Sid = "AllowRootAndHomeListingOfBucket",
                        Action = new string[] { "s3:ListBucket" },
                        Effect = "Allow",
                        Resource = policyRequests.Select(pr => pr.BucketName).ToArray(),
                        Condition = new Condition
                        {
                            StringEquals = new StringEquals
                            {
                                S3Prefix = pathList.ToArray(),
                                S3Delimiter = new string[] { "/" }
                            }
                        }
                    },
                    new Statement
                    {
                        Sid = "AllowListingOfUserFolder",
                        Action = new string[] { "s3:ListBucket" },
                        Effect = "Allow",
                        Resource = policyRequests.Select(pr => pr.BucketName).ToArray(),
                        Condition = new Condition
                        {
                            StringLike = new StringLike
                            {
                                S3Prefix = policyRequests.Select(pr => $"{pr.FolderName}/*")
                                .Union( policyRequests.Select(pr => $"{pr.FolderName}")).ToArray()
                            }
                        }
                    },
                    new Statement
                    {
                        Sid = "AllowAllS3ActionsInUserFolder",
                        Action = new string[] { "s3:*" },
                        Effect = "Allow",
                        Resource = policyRequests.Select(pr => $"{pr.BucketName}/{pr.FolderName}").ToArray(),
                    },
                }
            };
        }

        public static List<string> GetPathList(string? folderName)
        {
            Guard.Against.NullOrWhiteSpace(folderName, nameof(folderName));

            var pathList = new List<string> { folderName };

            var lastPath = folderName;

            while (lastPath.Contains("/"))
            {
                var path = lastPath.Substring(0, lastPath.LastIndexOf("/") + 1);
                pathList.Add(path);

                lastPath = lastPath.Substring(0, lastPath.LastIndexOf("/"));
            }

            pathList.Add("");

            return pathList;
        }

        public static string ToJson(this Policy self) => JsonConvert.SerializeObject(self, Converter.Settings);
    }
}
