// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Amazon.SecurityToken.Model;
using Monai.Deploy.Storage.MinIO.MinIoAdmin.Models;
using Monai.Deploy.Storage.S3Policy.Policies;

namespace Monai.Deploy.Storage.MinIO.MinIoAdmin.Interfaces
{
    public interface IMinioAdmin
    {
        /// <summary>
        /// Creates a user with read only permissions
        /// </summary>
        /// <param name="username"></param>
        /// <param name="policyRequests"></param>
        /// <returns></returns>
        Credentials CreateReadOnlyUser(string username, PolicyRequest[] policyRequests);

        bool HasConnection();

        void RemoveUser(string username);

        bool SetConnection();

        bool SetPolicy(MinioPolicyType policyType, List<string> policies, string itemName);

        bool UserAlreadyExists(string username);
    }
}
