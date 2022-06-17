// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Amazon.SecurityToken.Model;

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
        Task<Credentials> CreateUserAsync(string username, AccessPermissions permissions, string[] bucketNames);
    }
}
