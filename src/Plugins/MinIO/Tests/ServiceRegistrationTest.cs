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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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

        [Fact]
        public void AddMonaiDeployStorageService_WhenCalled_RegisterServicesOnly()
        {
            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));

            var returnedServiceCollection = serviceCollection.Object.AddMonaiDeployStorageService(_type.AssemblyQualifiedName, _fileSystem, HealthCheckOptions.None);

            Assert.Same(serviceCollection.Object, returnedServiceCollection);

            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.Exactly(4));
        }

        [Fact]
        public void AddMonaiDeployStorageService_WhenCalled_RegisterServicesAndServiceHealthCheck()
        {
            var serviceCollection = new Mock<IServiceCollection>();

            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));
            serviceCollection.Setup(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(HealthCheckService))));

            var returnedServiceCollection = serviceCollection.Object.AddMonaiDeployStorageService(_type.AssemblyQualifiedName, _fileSystem, HealthCheckOptions.ServiceHealthCheck);
            Assert.Same(serviceCollection.Object, returnedServiceCollection);

            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.AtLeast(5));
            serviceCollection.Verify(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(HealthCheckService))), Times.Once());
            serviceCollection.Verify(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(IConfigureOptions<HealthCheckServiceOptions>))), Times.Once());
        }

        [Fact]
        public void AddMonaiDeployStorageService_WhenCalled_RegisterServicesAndHealthCheck()
        {
            var serviceCollection = new Mock<IServiceCollection>();

            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));
            serviceCollection.Setup(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(HealthCheckService))));

            var returnedServiceCollection = serviceCollection.Object.AddMonaiDeployStorageService(_type.AssemblyQualifiedName, _fileSystem, HealthCheckOptions.AdminServiceHealthCheck);
            Assert.Same(serviceCollection.Object, returnedServiceCollection);

            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.AtLeast(5));
            serviceCollection.Verify(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(HealthCheckService))), Times.Once());
            serviceCollection.Verify(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(IConfigureOptions<HealthCheckServiceOptions>))), Times.Once());
        }

        [Fact]
        public void AddMonaiDeployStorageService_WhenCalled_RegisterServicesAndHealthChecks()
        {
            var serviceCollection = new Mock<IServiceCollection>();

            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));
            serviceCollection.Setup(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(HealthCheckService))));

            var returnedServiceCollection = serviceCollection.Object.AddMonaiDeployStorageService(_type.AssemblyQualifiedName, _fileSystem, HealthCheckOptions.ServiceHealthCheck | HealthCheckOptions.AdminServiceHealthCheck);
            Assert.Same(serviceCollection.Object, returnedServiceCollection);

            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.AtLeast(5));
            serviceCollection.Verify(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(HealthCheckService))), Times.Once());
            serviceCollection.Verify(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(IConfigureOptions<HealthCheckServiceOptions>))), Times.Exactly(2));
        }

        private static byte[] GetAssemblyeBytes(Assembly assembly)
        {
            return File.ReadAllBytes(assembly.Location);
        }
    }
}

#pragma warning restore CS8604 // Possible null reference argument.
