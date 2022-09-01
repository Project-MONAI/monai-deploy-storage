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

using System.IO.Abstractions;
using System.Reflection;
using Ardalis.GuardClauses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage
{
    [Flags]
    public enum HealthCheckOptions
    {
        None = 0,
        ServiceHealthCheck = 1,
        AdminServiceHealthCheck = 2,
    }

    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Configures all dependencies required for the MONAI Deploy Storage Service.
        /// </summary>
        /// <param name="services">Instance of <see cref="IServiceCollection"/>.</param>
        /// <param name="fullyQualifiedTypeName">Fully qualified type name of the service to use.</param>
        /// <returns>Instance of <see cref="IServiceCollection"/>.</returns>
        /// <exception cref="ConfigurationException"></exception>
        public static IServiceCollection AddMonaiDeployStorageService(
            this IServiceCollection services,
            string fullyQualifiedTypeName,
            HealthCheckOptions healthCheckOptions = HealthCheckOptions.ServiceHealthCheck | HealthCheckOptions.AdminServiceHealthCheck,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
            => AddMonaiDeployStorageService(services, fullyQualifiedTypeName, new FileSystem(), healthCheckOptions, failureStatus, tags, timeout);

        /// <summary>
        /// Configures all dependencies required for the MONAI Deploy Storage Service.
        /// </summary>
        /// <param name="services">Instance of <see cref="IServiceCollection"/>.</param>
        /// <param name="fullyQualifiedTypeName">Fully qualified type name of the service to use.</param>
        /// <param name="fileSystem">Instance of <see cref="IFileSystem"/>.</param>
        /// <returns>Instance of <see cref="IServiceCollection"/>.</returns>
        /// <exception cref="ConfigurationException"></exception>
        public static IServiceCollection AddMonaiDeployStorageService(
            this IServiceCollection services,
            string fullyQualifiedTypeName,
            IFileSystem fileSystem,
            HealthCheckOptions healthCheckOptions = HealthCheckOptions.ServiceHealthCheck & HealthCheckOptions.AdminServiceHealthCheck,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
        {
            Guard.Against.NullOrWhiteSpace(fullyQualifiedTypeName, nameof(fullyQualifiedTypeName));
            Guard.Against.Null(fileSystem, nameof(fileSystem));

            ResolveEventHandler resolveEventHandler = (sender, args) =>
            {
                return CurrentDomain_AssemblyResolve(args, fileSystem);
            };

            AppDomain.CurrentDomain.AssemblyResolve += resolveEventHandler;

            var storageServiceAssembly = LoadAssemblyFromDisk(GetAssemblyName(fullyQualifiedTypeName), fileSystem);

            if (!IsSupportedType(fullyQualifiedTypeName, storageServiceAssembly))
            {
                throw new ConfigurationException($"The configured type '{fullyQualifiedTypeName}' does not implement the {typeof(IStorageService).Name} interface.");
            }

            RegisterServices(services, fullyQualifiedTypeName, storageServiceAssembly);

            RegisterHealtChecks(healthCheckOptions, services, fullyQualifiedTypeName, storageServiceAssembly, failureStatus, tags, timeout);

            AppDomain.CurrentDomain.AssemblyResolve -= resolveEventHandler;
            return services;
        }

        private static void RegisterHealtChecks(
            HealthCheckOptions healthCheckOptions,
            IServiceCollection services,
            string fullyQualifiedTypeName,
            Assembly storageServiceAssembly,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
        {
            if (healthCheckOptions == HealthCheckOptions.None)
            {
                return;
            }

            var healthCheckBase = storageServiceAssembly.GetTypes().FirstOrDefault(p => p.IsSubclassOf(typeof(HealthCheckRegistrationBase)));

            if (healthCheckBase is null || Activator.CreateInstance(healthCheckBase) is not HealthCheckRegistrationBase healthCheckBuilderBase)
            {
                throw new ConfigurationException($"Health check registrar cannot be found for the configured plug-in '{fullyQualifiedTypeName}'.");
            }

            var healthCheckBuilder = services.AddHealthChecks();

            if (healthCheckOptions.HasFlag(HealthCheckOptions.ServiceHealthCheck))
            {
                healthCheckBuilderBase.ConfigureHealthCheck(healthCheckBuilder, failureStatus, tags, timeout);
            }

            if (healthCheckOptions.HasFlag(HealthCheckOptions.AdminServiceHealthCheck))
            {
                healthCheckBuilderBase.ConfigureAdminHealthCheck(healthCheckBuilder, failureStatus, tags, timeout);
            }
        }

        private static void RegisterServices(IServiceCollection services, string fullyQualifiedTypeName, Assembly storageServiceAssembly)
        {
            var serviceRegistrationType = storageServiceAssembly.GetTypes().FirstOrDefault(p => p.IsSubclassOf(typeof(ServiceRegistrationBase)));
            if (serviceRegistrationType is null || Activator.CreateInstance(serviceRegistrationType) is not ServiceRegistrationBase serviceRegistrar)
            {
                throw new ConfigurationException($"Service registrar cannot be found for the configured plug-in '{fullyQualifiedTypeName}'.");
            }

            serviceRegistrar.Configure(services);
        }

        internal static bool IsSupportedType(string fullyQualifiedTypeName, Assembly storageServiceAssembly)
        {
            Guard.Against.NullOrWhiteSpace(fullyQualifiedTypeName, nameof(fullyQualifiedTypeName));
            Guard.Against.Null(storageServiceAssembly, nameof(storageServiceAssembly));

            var storageServiceType = Type.GetType(fullyQualifiedTypeName, assemblyeName => storageServiceAssembly, null, false);

            return storageServiceType is not null &&
                storageServiceType.GetInterfaces().Contains(typeof(IStorageService));
        }

        internal static string GetAssemblyName(string fullyQualifiedTypeName)
        {
            var assemblyNameParts = fullyQualifiedTypeName.Split(',', StringSplitOptions.None);
            if (assemblyNameParts.Length < 2 || string.IsNullOrWhiteSpace(assemblyNameParts[1]))
            {
                throw new ConfigurationException($"The configured storage service type '{fullyQualifiedTypeName}' is not a valid fully qualified type name.  E.g. {StorageServiceConfiguration.DefaultStorageServiceAssemblyName}")
                {
                    HelpLink = "https://docs.microsoft.com/en-us/dotnet/standard/assembly/find-fully-qualified-name"
                };
            }

            return assemblyNameParts[1].Trim();
        }

        internal static Assembly CurrentDomain_AssemblyResolve(ResolveEventArgs args, IFileSystem fileSystem)
        {
            Guard.Against.Null(args, nameof(args));

            var requestedAssemblyName = new AssemblyName(args.Name);
            return LoadAssemblyFromDisk(requestedAssemblyName.Name!, fileSystem);
        }

        internal static Assembly LoadAssemblyFromDisk(string assemblyName, IFileSystem fileSystem)
        {
            Guard.Against.NullOrWhiteSpace(assemblyName, nameof(assemblyName));
            Guard.Against.Null(fileSystem, nameof(fileSystem));

            if (!fileSystem.Directory.Exists(SR.PlugInDirectoryPath))
            {
                throw new ConfigurationException($"Plug-in directory '{SR.PlugInDirectoryPath}' cannot be found.");
            }

            var assemblyFilePath = fileSystem.Path.Combine(SR.PlugInDirectoryPath, $"{assemblyName}.dll");
            if (!fileSystem.File.Exists(assemblyFilePath))
            {
                throw new ConfigurationException($"The configured storage plug-in '{assemblyFilePath}' cannot be found.");
            }

            var asesmblyeData = fileSystem.File.ReadAllBytes(assemblyFilePath);
            return Assembly.Load(asesmblyeData);
        }
    }
}
