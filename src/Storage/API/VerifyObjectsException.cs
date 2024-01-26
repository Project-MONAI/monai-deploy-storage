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

namespace Monai.Deploy.Storage.API
{
    public class VerifyObjectsException : Exception
    {
        private readonly List<Exception> _errors;
        private readonly Dictionary<string, bool> _results;

        public IReadOnlyList<Exception> Exceptions
        { get { return _errors; } }
        public IReadOnlyDictionary<string, bool> Results
        { get { return _results; } }

        public VerifyObjectsException()
        {
            _errors = [];
            _results = [];
        }

        public VerifyObjectsException(string? message) : base(message)
        {
            _errors = [];
            _results = [];
        }

        public VerifyObjectsException(string? message, Exception? innerException) : base(message, innerException)
        {
            _errors = [];
            _results = [];
        }

        public VerifyObjectsException(List<Exception> errors, Dictionary<string, bool> files)
        {
            _errors = errors;
            _results = files;
        }
    }
}
