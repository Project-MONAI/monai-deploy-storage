// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System;
using System.Threading.Tasks;
using Amazon.SecurityToken.Model;
using Monai.Deploy.Storage.API;

namespace Monai.Deploy.Storage.AWSS3
{
    public class StorageAdminService : IStorageAdminService
    {
        public Task<Credentials> CreateUserAsync(string username, AccessPermissions permissions, string[] bucketNames) => throw new NotImplementedException();
    }
}
