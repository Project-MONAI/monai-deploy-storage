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

namespace Monai.Deploy.Storage.Common
{
    public static partial class Logger
    {
        [LoggerMessage(EventId = 20000, Level = LogLevel.Error, Message = "Error listing objects in bucket '{bucketName}'.")]
        public static partial void ListObjectError(this ILogger logger, string bucketName);

        [LoggerMessage(EventId = 20001, Level = LogLevel.Error, Message = "File '{path}' could not be found in '{bucketName}'.")]
        public static partial void FileNotFoundError(this ILogger logger, string bucketName, string path);
    }
}
