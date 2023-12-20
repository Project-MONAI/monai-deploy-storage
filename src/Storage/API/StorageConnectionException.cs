﻿/*
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

namespace Monai.Deploy.Storage.API
{
    public class StorageConnectionException : Exception
    {
        public string ServerMessage { get; set; } = default!;
        public List<string> Errors { get; set; }

        public StorageConnectionException()
        {
            Errors = new List<string>();
        }

        public StorageConnectionException(string message) : base(message)
        {
            Errors = new List<string>();
        }

        public StorageConnectionException(string message, Exception innerException) : base(message, innerException)
        {
            Errors = new List<string>();
        }
    }
}
