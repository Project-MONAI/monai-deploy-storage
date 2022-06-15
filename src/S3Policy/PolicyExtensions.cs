// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
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
                            StringEquals = new StringEquals
                            {
                                S3Prefix = policyRequests.Select(pr => $"{pr.FolderName}/*").ToArray(),
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
