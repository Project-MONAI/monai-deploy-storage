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

using Microsoft.Extensions.Logging;
using Minio.Exceptions;

namespace Monai.Deploy.Storage.MinIO
{
    public static partial class LoggerMethods
    {
        [LoggerMessage(EventId = 20000, Level = LogLevel.Error, Message = "Error listing objects in bucket '{bucketName}' with error: {error}")]
        public static partial void ListObjectError(this ILogger logger, string bucketName, string error);

        [LoggerMessage(EventId = 20001, Level = LogLevel.Error, Message = "File '{path}' could not be found in '{bucketName}'.")]
        public static partial void FileNotFoundError(this ILogger logger, string bucketName, string path);

        [LoggerMessage(EventId = 20002, Level = LogLevel.Error, Message = "Error verifying objects in bucket '{bucketName}'.")]
        public static partial void VerifyObjectError(this ILogger logger, string bucketName, Exception ex);

        [LoggerMessage(EventId = 20003, Level = LogLevel.Error, Message = "Health check failure.")]
        public static partial void HealthCheckError(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 20004, Level = LogLevel.Debug, Message = "Temporary credential policy={policy}.")]
        public static partial void TemporaryCredentialPolicy(this ILogger logger, string policy);

        [LoggerMessage(EventId = 20005, Level = LogLevel.Information, Message = "`createBuckets` not configured; no buckets created.")]
        public static partial void NoBucketCreated(this ILogger logger);

        [LoggerMessage(EventId = 20006, Level = LogLevel.Critical, Message = "Error creating bucket {bucket} in region {region}.")]
        public static partial void ErrorCreatingBucket(this ILogger logger, string bucket, string region, Exception ex);

        [LoggerMessage(EventId = 20007, Level = LogLevel.Information, Message = "Bucket {bucket} created in region {region}.")]
        public static partial void BucketCreated(this ILogger logger, string bucket, string region);

        [LoggerMessage(EventId = 20008, Level = LogLevel.Error, Message = "Error connecting to MinIO.")]
        public static partial void ConnectionError(this ILogger logger, ConnectionException ex);

        [LoggerMessage(EventId = 20009, Level = LogLevel.Error, Message = "Storage service error.")]
        public static partial void StorageServiceError(this ILogger logger, Exception ex);
    }
}
