// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Ardalis.GuardClauses;

namespace Monai.Deploy.Storage.S3Policy.Policies
{
    public class PolicyRequest
    {
        private readonly string _bucketName;

        public PolicyRequest(string bucketName, string folderName)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));
            _bucketName = bucketName;
            FolderName = folderName;
        }

        public string BucketName { get => $"arn:aws:s3:::{_bucketName}"; }

        public string FolderName { get; } = "";
    }
}
