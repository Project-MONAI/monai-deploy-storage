// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Microsoft.Extensions.DependencyInjection;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.MinIO.MinIoAdmin;
using Monai.Deploy.Storage.MinIO.MinIoAdmin.Interfaces;

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
            services.AddSingleton<IAmazonSecurityTokenServiceClientFactory, AmazonSecurityTokenServiceClientFactory>();
            services.AddSingleton<IStorageService, MinIoStorageService>();
            services.AddSingleton<IMinioAdmin, Shell>();
            return services;
        }
    }
}
