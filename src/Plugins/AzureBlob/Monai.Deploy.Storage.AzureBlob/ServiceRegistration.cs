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

using Microsoft.Extensions.DependencyInjection;

namespace Monai.Deploy.Storage.AzureBlob
{
    public class ServiceRegistration : ServiceRegistrationBase
    {
        public override IServiceCollection Configure(IServiceCollection services)
        {
            services.AddSingleton<IAzureBlobClientFactory, AzureBlobClientFactory>();
            services.AddSingleton<AzureBlobStartup>();
            services.AddHostedService(p => p.GetRequiredService<AzureBlobStartup>());
            return services;
        }
    }
}
