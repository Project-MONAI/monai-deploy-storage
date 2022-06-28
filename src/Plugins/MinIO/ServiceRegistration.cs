// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Microsoft.Extensions.DependencyInjection;
using Monai.Deploy.Storage.API;

namespace Monai.Deploy.Storage.MinIO
{
    public class ServiceRegistration : ServiceRegistrationBase
    {
        public ServiceRegistration(string fullyQualifiedAssemblyName) : base(fullyQualifiedAssemblyName)
        {
        }

        public override IServiceCollection Configure(IServiceCollection services)
        {
            services.AddSingleton<IMinIoClientFactory, MinIoClientFactory>();
            services.AddSingleton<IAmazonSecurityTokenServiceClientFactory, AmazonSecurityTokenServiceClientFactory>();
            services.AddSingleton<IStorageService, MinIoStorageService>();
            services.AddSingleton<IStorageAdminService, StorageAdminService>();
            return services;
        }
    }
}
