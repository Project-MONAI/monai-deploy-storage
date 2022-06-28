// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Microsoft.Extensions.Logging;

namespace Monai.Deploy.Storage.MinIO
{
    public static partial class LoggerMethods
    {
        [LoggerMessage(EventId = 20000, Level = LogLevel.Error, Message = "Error listing objects in bucket '{bucketName}'.")]
        public static partial void ListObjectError(this ILogger logger, string bucketName);

        [LoggerMessage(EventId = 20001, Level = LogLevel.Error, Message = "File '{path}' could not be found in '{bucketName}'.")]
        public static partial void FileNotFoundError(this ILogger logger, string bucketName, string path);

        [LoggerMessage(EventId = 20002, Level = LogLevel.Error, Message = "Error verifying objects in bucket '{bucketName}'.")]
        public static partial void VerifyObjectError(this ILogger logger, string bucketName, Exception ex);
    }
}
