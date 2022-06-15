// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System.Reflection;
using Ardalis.GuardClauses;
using Microsoft.Extensions.DependencyInjection;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddMonaiDeployStorageService(this IServiceCollection services, string fullyQualifiedTypeName)
        {
            Guard.Against.NullOrWhiteSpace(fullyQualifiedTypeName, nameof(fullyQualifiedTypeName));

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            var storageServiceAssembly = LoadAssemblyFromDisk(GetAssemblyName(fullyQualifiedTypeName));
            var serviceRegistrationType = storageServiceAssembly.GetTypes().First(p => p.IsSubclassOf(typeof(ServiceRegistrationBase)));

            if (serviceRegistrationType is null || Activator.CreateInstance(serviceRegistrationType, fullyQualifiedTypeName) is not ServiceRegistrationBase serviceRegistrar)
            {
                throw new ConfigurationException($"Service registrar cannot be found for the configured plug-in '{fullyQualifiedTypeName}'.");
            }

            serviceRegistrar.Configure(services);

            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            return services;
        }

        private static string GetAssemblyName(string fullyQualifiedTypeName)
        {
            var assemblyNameParts = fullyQualifiedTypeName.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (assemblyNameParts.Length != 2)
            {
                throw new ConfigurationException($"The configured storage service type is not a valid fully qualified type name.  E.g. {StorageServiceConfiguration.DefaultStorageServiceAssemblyName}");
            }

            return assemblyNameParts[1].Trim();
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var requestedAssemblyName = new AssemblyName(args.Name);
            return LoadAssemblyFromDisk(requestedAssemblyName.Name);
        }

        private static Assembly LoadAssemblyFromDisk(object assemblyName)
        {
            if (!Directory.Exists(SR.PlugInDirectoryPath))
            {
                throw new ConfigurationException($"Plug-in directory '{SR.PlugInDirectoryPath}' cannot be found.");
            }

            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, $"{assemblyName}.dll");
            if (!File.Exists(assemblyFilePath))
            {
                throw new ConfigurationException($"The configured storage plug-in '{assemblyFilePath}' cannot be found.");
            }

            var asesmblyeData = File.ReadAllBytes(assemblyFilePath);
            return Assembly.Load(asesmblyeData);
        }
    }
}
