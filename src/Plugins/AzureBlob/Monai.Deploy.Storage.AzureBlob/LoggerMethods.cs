/*
 * Copyright 2023 MONAI Consortium
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

using Microsoft.Extensions.Logging;

namespace Monai.Deploy.Storage.AzureBlob
{
    public static partial class LoggerMethods
    {
        [LoggerMessage(EventId = 20000, Level = LogLevel.Error, Message = "Error listing objects in container '{containerName}' with error: {error}")]
        public static partial void ListObjectError(this ILogger logger, string containerName, string error);

        [LoggerMessage(EventId = 20001, Level = LogLevel.Error, Message = "File '{path}' could not be found in '{containerName}'.")]
        public static partial void FileNotFoundError(this ILogger logger, string containerName, string path);

        [LoggerMessage(EventId = 20002, Level = LogLevel.Error, Message = "Error verifying objects in container '{containerName}'.")]
        public static partial void VerifyObjectError(this ILogger logger, string containerName, Exception ex);

        [LoggerMessage(EventId = 20003, Level = LogLevel.Error, Message = "Health check failure.")]
        public static partial void HealthCheckError(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 20004, Level = LogLevel.Debug, Message = "Temporary credential policy={policy}.")]
        public static partial void TemporaryCredentialPolicy(this ILogger logger, string policy);

        [LoggerMessage(EventId = 20005, Level = LogLevel.Information, Message = "`createcontainers` not configured; no containers created.")]
        public static partial void NoContainerCreated(this ILogger logger);

        [LoggerMessage(EventId = 20006, Level = LogLevel.Critical, Message = "Error creating container {container}.")]
        public static partial void ErrorCreatingContainer(this ILogger logger, string container, Exception ex);

        [LoggerMessage(EventId = 20007, Level = LogLevel.Information, Message = "container {container} created")]
        public static partial void ContainerCreated(this ILogger logger, string container);

        [LoggerMessage(EventId = 20009, Level = LogLevel.Error, Message = "Storage service error.")]
        public static partial void StorageServiceError(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 20010, Level = LogLevel.Debug, Message = "Copied from {sourceContainer}/{sourcePath} to {destinationContainer}/{destinationPath}")]
        public static partial void BlobCopied(this ILogger logger, string sourceContainer, string sourcePath, string destinationContainer, string destinationPath);

        [LoggerMessage(EventId = 20011, Level = LogLevel.Debug, Message = "Returning stream from {sourceContainer}/{sourcePath}")]
        public static partial void BlobGetObject(this ILogger logger, string sourceContainer, string sourcePath);

        [LoggerMessage(EventId = 20012, Level = LogLevel.Trace, Message = "Returning file list from {sourceContainer} with prefix {prefix}")]
        public static partial void BlobListObjects(this ILogger logger, string sourceContainer, string? prefix);

        [LoggerMessage(EventId = 20013, Level = LogLevel.Debug, Message = "Uploaded file {sourceContainer}/{path}")]
        public static partial void BlobPutObject(this ILogger logger, string sourceContainer, string path);

        [LoggerMessage(EventId = 20014, Level = LogLevel.Debug, Message = "Remove file {sourceContainer}/{path}")]
        public static partial void BlobRemoveObject(this ILogger logger, string sourceContainer, string path);

        [LoggerMessage(EventId = 20015, Level = LogLevel.Debug, Message = "Remove a list of files from {sourceContainer}")]
        public static partial void BlobRemoveObjects(this ILogger logger, string sourceContainer);

        [LoggerMessage(EventId = 20016, Level = LogLevel.Error, Message = "container {container} does not exist")]
        public static partial void ContainerDoesNotExistCreated(this ILogger logger, string container);

    }
}
