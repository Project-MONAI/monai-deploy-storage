// SPDX-FileCopyrightText: � 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monai.Deploy.Storage.Configuration;
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.MinIO.Tests
{
#pragma warning disable CS8604 // Possible null reference argument.

    public class ServiceRegistrationTest
    {
        private readonly Type _type;
        private readonly MockFileSystem _fileSystem;

        public ServiceRegistrationTest()
        {
            _type = typeof(MinIoStorageService);
            _fileSystem = new MockFileSystem();
            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, _type.Assembly.ManifestModule.Name);
            var assemblyData = GetAssemblyeBytes(_type.Assembly);
            _fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            _fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);
        }

        [Fact(DisplayName = "Shall be able to Add MinIO as default storage service")]
        public void ShallAddMinIOAsDefaultStorageService()
        {
            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));

            var returnedServiceCollection = serviceCollection.Object.AddMonaiDeployStorageService(_type.AssemblyQualifiedName, _fileSystem);

            Assert.Same(serviceCollection.Object, returnedServiceCollection);

            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.Exactly(4));
        }

        private void AddOptions(StorageServiceConfiguration options, string[] requiredKeys)
        {
            foreach (var key in requiredKeys)
            {
                if (options.Settings.ContainsKey(key)) continue;

                options.Settings.Add(key, Guid.NewGuid().ToString());
            }
        }

        private static byte[] GetAssemblyeBytes(Assembly assembly)
        {
            return File.ReadAllBytes(assembly.Location);
        }
    }

#pragma warning restore CS8604 // Possible null reference argument.
}
