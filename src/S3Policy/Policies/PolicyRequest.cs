/*
 * Copyright 2021-2024 MONAI Consortium
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Ardalis.GuardClauses;

namespace Monai.Deploy.Storage.S3Policy.Policies
{
    public class PolicyRequest
    {
        private readonly string _bucketName;

        public string BucketName { get => $"arn:aws:s3:::{_bucketName}"; }

        public string FolderName { get; }

        public PolicyRequest(string bucketName, string folderName)
        {
            Guard.Against.NullOrWhiteSpace(bucketName, nameof(bucketName));

            _bucketName = bucketName;
            FolderName = folderName;
        }
    }
}
