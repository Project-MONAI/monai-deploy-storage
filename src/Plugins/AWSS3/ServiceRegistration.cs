// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Microsoft.Extensions.DependencyInjection;
using Monai.Deploy.Storage.API;

namespace Monai.Deploy.Storage.AWSS3
{
    public class ServiceRegistration : ServiceRegistrationBase
    {
        public ServiceRegistration(string fullyQualifiedAssemblyName) : base(fullyQualifiedAssemblyName)
        {
        }

        public override IServiceCollection Configure(IServiceCollection services)
        {
            services.AddSingleton<IStorageService, Awss3StorageService>();
            return services;
        }
    }
}
