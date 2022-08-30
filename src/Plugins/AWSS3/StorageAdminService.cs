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

using System;
using System.Threading.Tasks;
using Amazon.SecurityToken.Model;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.S3Policy.Policies;

namespace Monai.Deploy.Storage.AWSS3
{
    public class StorageAdminService : IStorageAdminService
    {
        public Task<Credentials> CreateUserAsync(string username, AccessPermissions permissions, string[] bucketNames) => throw new NotImplementedException();

        public Task<Credentials> CreateUserAsync(string username, PolicyRequest[] policyRequests) => throw new NotImplementedException();

        public Task RemoveUserAsync(string username) => throw new NotImplementedException();
    }
}
