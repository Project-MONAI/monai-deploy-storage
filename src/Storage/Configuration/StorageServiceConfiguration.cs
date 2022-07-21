/*
 * Copyright 2021-2022 MONAI Consortium
 * Copyright 2019-2021 NVIDIA Corporation
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

using Microsoft.Extensions.Configuration;

namespace Monai.Deploy.Storage.Configuration
{
    public class StorageServiceConfiguration
    {
        internal static readonly string DefaultStorageServiceAssemblyName = "Monai.Deploy.Storage.MinIo.MinIoStorageService, Monai.Deploy.Storage.MinIo";

        /// <summary>
        /// Gets or sets the a fully qualified type name of the storage service.
        /// The specified type must implement <typeparam name="Monai.Deploy.Storage.IStorageService">IStorageService</typeparam> interface.
        /// The default storage service configured is MinIO.
        /// </summary>
        [ConfigurationKeyName("serviceAssemblyName")]
        public string ServiceAssemblyName { get; set; } = DefaultStorageServiceAssemblyName;

        /// <summary>
        /// Gets or sets the storage service settings.
        /// Service implementer shall validate settings in the constructor and specify all settings in a single level JSON object as in the example below.
        /// </summary>
        /// <example>
        /// <code>
        /// {
        ///     ...
        ///     "settings": {
        ///         "endpoint": "1.2.3.4",
        ///         "accessKey": "monaideploy",
        ///         "accessToken": "mysecret",
        ///         "securedConnection": true,
        ///         "bucket": "myBucket"
        ///     }
        /// }
        /// </code>
        /// </example>
        [ConfigurationKeyName("settings")]
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }
}
