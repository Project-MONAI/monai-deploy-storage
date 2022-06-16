// SPDX-FileCopyrightText: � 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Monai.Deploy.Storage;
using Monai.Deploy.Storage.MinIO;
using Moq;
using Xunit;

namespace Tests
{
#pragma warning disable CS8604 // Possible null reference argument.
    public class ServiceRegistrationTest
    {
        [Fact(DisplayName = "Shall be able to Add MinIO as default storage service")]
        public void ShallAddMinIOAsDefaultStorageService()
        {
            var type = typeof(MinIoStorageService);

            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));

            var fileSystem = new MockFileSystem();

            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, type.Assembly.ManifestModule.Name);
            var assemblyData = GetAssemblyeBytes(type.Assembly);
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);

            var returnedServiceCollection = serviceCollection.Object.AddMonaiDeployStorageService(type.AssemblyQualifiedName, fileSystem);

            Assert.Same(serviceCollection.Object, returnedServiceCollection);

            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.Exactly(5));
        }

        private static byte[] GetAssemblyeBytes(Assembly assembly)
        {
            return File.ReadAllBytes(assembly.Location);
        }
    }
#pragma warning restore CS8604 // Possible null reference argument.
}
