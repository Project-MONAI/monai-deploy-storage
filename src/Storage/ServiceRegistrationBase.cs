// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Ardalis.GuardClauses;
using Microsoft.Extensions.DependencyInjection;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage
{
    public abstract class ServiceRegistrationBase
    {
        protected string FullyQualifiedAssemblyName { get; }
        protected string AssemblyFilename { get; }

        protected ServiceRegistrationBase(string fullyQualifiedAssemblyName)
        {
            Guard.Against.NullOrWhiteSpace(fullyQualifiedAssemblyName, nameof(fullyQualifiedAssemblyName));
            FullyQualifiedAssemblyName = fullyQualifiedAssemblyName;
            AssemblyFilename = ParseAssemblyName();
        }

        private string ParseAssemblyName()
        {
            var assemblyNameParts = FullyQualifiedAssemblyName.Split(',', StringSplitOptions.None);
            if (assemblyNameParts.Length <= 2 || string.IsNullOrWhiteSpace(assemblyNameParts[1]))
            {
                throw new ConfigurationException($"Storage service '{FullyQualifiedAssemblyName}' is invalid.  Please provide a fully qualified name.")
                {
                    HelpLink = "https://docs.microsoft.com/en-us/dotnet/standard/assembly/find-fully-qualified-name"
                };
            }

            return assemblyNameParts[1].Trim();
        }

        public abstract IServiceCollection Configure(IServiceCollection services);
    }
}
