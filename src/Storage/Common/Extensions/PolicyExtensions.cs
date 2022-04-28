// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Ardalis.GuardClauses;
using Monai.Deploy.Storage.Common.Policies;

namespace Monai.Deploy.Storage.Common.Extensions
{
    public static class PolicyExtensions
    {
        public static Policy ToPolicy(string bucketName, string folderName)
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

        public static List<string> GetPathList(string folderName)
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
    }
}
