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

namespace Monai.Deploy.Storage.API
{
    public class ListObjectException : Exception
    {
        private readonly List<VirtualFileInfo> _files;

        public Exception? Exception { get; }
        public IReadOnlyList<VirtualFileInfo> Files
        { get { return _files; } }

        public ListObjectException()
        {
            _files = new List<VirtualFileInfo>();
        }

        public ListObjectException(string? message) : base(message)
        {
            _files = new List<VirtualFileInfo>();
        }

        public ListObjectException(string? message, Exception? innerException) : base(message, innerException)
        {
            _files = new List<VirtualFileInfo>();
        }

        public ListObjectException(Exception exception, List<VirtualFileInfo> files)
        {
            Exception = exception;
            _files = files;
        }
    }
}
