/*
 * Copyright 2022 MONAI Consortium
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

using Amazon.SecurityToken.Model;
using Monai.Deploy.Storage.S3Policy.Policies;

namespace Monai.Deploy.Storage.API
{
    public interface IStorageAdminService
    {
        /// <summary>
        /// Creates a user with read only permissions
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="permissions">User permissions</param>
        /// <param name="bucketNames">Name of the bucket that the user needs access to</param>
        /// <returns></returns>
        [Obsolete("CreateUserAsync with bucketNames is deprecated, please use CreateUserAsync with an array of PolicyRequest instead.")]
        Task<Credentials> CreateUserAsync(string username, AccessPermissions permissions, string[] bucketNames);

        /// <summary>
        /// Creates a user with read only permissions
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="policyRequests">Contains the buckets and folders that the user needs access to</param>
        /// <returns></returns>
        Task<Credentials> CreateUserAsync(string username, PolicyRequest[] policyRequests);
    }
}
