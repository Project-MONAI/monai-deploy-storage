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

namespace Monai.Deploy.Storage.SimpleStorage
{
    public static partial class LoggerMethods
    {
        [LoggerMessage(EventId = 20000, Level = LogLevel.Error, Message = "File {path} is corrupted, using copy {pathCopy}, replaceing main with copy")]
        public static partial void MainFileCorrupt(this ILogger logger, string path, string pathCopy);

        [LoggerMessage(EventId = 20001, Level = LogLevel.Debug, Message = "Removed file/folder {objectName} from bucket {bucketName}")]
        public static partial void RemovedFile(this ILogger logger, string objectName, string bucketName);

        [LoggerMessage(EventId = 20002, Level = LogLevel.Error, Message = "path cannot be empty! filename: {objectName} bucket: {bucketName}")]
        public static partial void FileCannotBeEmpty(this ILogger logger, string objectName, string bucketName);

        [LoggerMessage(EventId = 20003, Level = LogLevel.Warning, Message = "Retrying {AttemptNumber} : {Message}")]
        public static partial void FileWriteErrorRetrying(this ILogger logger, int AttemptNumber, string? Message);

        [LoggerMessage(EventId = 20004, Level = LogLevel.Warning, Message = "HealthCheck failed to list buckets ! {exceptionMessage}")]
        public static partial void HealthCheckError(this ILogger logger, Exception exception, string exceptionMessage);
    }
}
