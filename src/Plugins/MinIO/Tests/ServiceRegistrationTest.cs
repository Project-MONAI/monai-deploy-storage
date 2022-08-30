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

using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
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

            var returnedServiceCollection = serviceCollection.Object.AddMonaiDeployStorageService(_type.AssemblyQualifiedName, _fileSystem, false);

            Assert.Same(serviceCollection.Object, returnedServiceCollection);

            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.Exactly(4));
        }

        private static byte[] GetAssemblyeBytes(Assembly assembly)
        {
            return File.ReadAllBytes(assembly.Location);
        }
    }

#pragma warning restore CS8604 // Possible null reference argument.
}
