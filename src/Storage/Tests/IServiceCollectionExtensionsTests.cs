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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;
using Moq;
using Xunit;

namespace Monai.Deploy.Storage.Tests
{
#pragma warning disable CS8604 // Possible null reference argument.

    public class IServiceCollectionExtensionsTests
    {
        [Theory(DisplayName = "AddMonaiDeployStorageService throws when type name is invalid")]
        [InlineData("mytype")]
        [InlineData("mytype,, myversion")]
        [InlineData("mytype, myassembly, myversion")]
        public void AddMonaiDeployStorageService_ThrowsOnInvalidTypeName(string typeName)
        {
            var serviceCollection = new Mock<IServiceCollection>();

            Assert.Throws<ConfigurationException>(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, new MockFileSystem()));
        }

        [Fact(DisplayName = "AddMonaiDeployStorageService throws if the plug-ins directory is missing")]
        public void AddMonaiDeployStorageService_ThrowsIfPlugInsDirectoryIsMissing()
        {
            var typeName = typeof(TheBadTestStorageService).AssemblyQualifiedName;
            var serviceCollection = new Mock<IServiceCollection>();
            var exception = Assert.Throws<ConfigurationException>(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, new MockFileSystem()));

            Assert.NotNull(exception);
            Assert.Equal($"Plug-in directory '{SR.PlugInDirectoryPath}' cannot be found.", exception.Message);
        }

        [Fact(DisplayName = "AddMonaiDeployStorageService throws if the plug-in dll is missing")]
        public void AddMonaiDeployStorageService_ThrowsIfPlugInDllIsMissing()
        {
            var badType = typeof(TheBadTestStorageService);
            var typeName = badType.AssemblyQualifiedName;
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            var serviceCollection = new Mock<IServiceCollection>();
            var exception = Assert.Throws<ConfigurationException>(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, fileSystem));

            Assert.NotNull(exception);
            Assert.Equal($"The configured storage plug-in '{SR.PlugInDirectoryPath}{Path.DirectorySeparatorChar}{badType.Assembly.ManifestModule.Name}' cannot be found.", exception.Message);
        }

        [Fact(DisplayName = "AddMonaiDeployStorageService throws if storage service type is not supported")]
        public void AddMonaiDeployStorageService_ThrowsIfStorageServiceTypeIsNotSupported()
        {
            var badType = typeof(TheBadTestStorageService);
            var typeName = badType.AssemblyQualifiedName;
            var assemblyData = GetAssemblyeBytes(badType.Assembly);
            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, badType.Assembly.ManifestModule.Name);
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);
            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Clear());
            var exception = Record.Exception(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, fileSystem));

            Assert.NotNull(exception);
            Assert.Equal($"The configured type '{typeName}' does not implement the {typeof(IStorageService).Name} interface.", exception.Message);
        }

        [Fact(DisplayName = "AddMonaiDeployStorageService configures all services as expected")]
        public void AddMonaiDeployStorageService_ConfiuresServicesAsExpected()
        {
            var goodType = typeof(GoodStorageService);
            var typeName = goodType.AssemblyQualifiedName;
            var assemblyData = GetAssemblyeBytes(goodType.Assembly);
            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, goodType.Assembly.ManifestModule.Name);
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);
            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Clear());
            var exception = Record.Exception(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, fileSystem, HealthCheckOptions.None));

            Assert.Null(exception);

            serviceCollection.Verify(p => p.Clear(), Times.Once());
        }

        [Fact(DisplayName = "AddMonaiDeployStorageService configures all services & service health check as expected")]
        public void AddMonaiDeployStorageService_ConfiuresServicesAndServiceHealtCheckAsExpected()
        {
            var goodType = typeof(GoodStorageService);
            var typeName = goodType.AssemblyQualifiedName;
            var assemblyData = GetAssemblyeBytes(goodType.Assembly);
            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, goodType.Assembly.ManifestModule.Name);
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);

            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Clear());
            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));

            var exception = Record.Exception(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, fileSystem, HealthCheckOptions.ServiceHealthCheck));

            Assert.Null(exception);

            serviceCollection.Verify(p => p.Clear(), Times.Once());
            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.Exactly(2));
        }

        [Fact(DisplayName = "AddMonaiDeployStorageService configures all services & admin health check as expected")]
        public void AddMonaiDeployStorageService_ConfiuresServicesAndAdminHealthCheckAsExpected()
        {
            var goodType = typeof(GoodStorageService);
            var typeName = goodType.AssemblyQualifiedName;
            var assemblyData = GetAssemblyeBytes(goodType.Assembly);
            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, goodType.Assembly.ManifestModule.Name);
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);

            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Clear());
            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));

            var exception = Record.Exception(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, fileSystem, HealthCheckOptions.AdminServiceHealthCheck));

            Assert.Null(exception);

            serviceCollection.Verify(p => p.Clear(), Times.Once());
            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.Exactly(2));
        }

        [Fact(DisplayName = "AddMonaiDeployStorageService configures all services & all health checks as expected")]
        public void AddMonaiDeployStorageService_ConfiuresServicesAndAllHealtCheckAsExpected()
        {
            var goodType = typeof(GoodStorageService);
            var typeName = goodType.AssemblyQualifiedName;
            var assemblyData = GetAssemblyeBytes(goodType.Assembly);
            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, goodType.Assembly.ManifestModule.Name);
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);

            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Clear());
            serviceCollection.Setup(p => p.Add(It.IsAny<ServiceDescriptor>()));

            var exception = Record.Exception(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, fileSystem, HealthCheckOptions.ServiceHealthCheck | HealthCheckOptions.AdminServiceHealthCheck));

            Assert.Null(exception);

            serviceCollection.Verify(p => p.Clear(), Times.Once());
            serviceCollection.Verify(p => p.Add(It.IsAny<ServiceDescriptor>()), Times.Exactly(2));
            serviceCollection.Verify(p => p.Add(It.Is<ServiceDescriptor>(p => p.ServiceType == typeof(HealthCheckService))), Times.Once());
        }

        private static byte[] GetAssemblyeBytes(Assembly assembly)
        {
            return File.ReadAllBytes(assembly.Location);
        }
    }

    internal class TestHealthCheckRegistrar : HealthCheckRegistrationBase
    {
        public override IHealthChecksBuilder ConfigureAdminHealthCheck(IHealthChecksBuilder builder, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, TimeSpan? timeout = null) => builder;

        public override IHealthChecksBuilder ConfigureHealthCheck(IHealthChecksBuilder builder, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, TimeSpan? timeout = null) => builder;
    }

    internal class TestServiceRegistrar : ServiceRegistrationBase
    {
        public override IServiceCollection Configure(IServiceCollection services)
        {
            services.Clear();
            return services;
        }
    }

    internal class GoodStorageService : IStorageService
    {
        public string Name => "Test Storage Service";

        public Task CopyObjectAsync(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task CopyObjectWithCredentialsAsync(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task CreateFolderAsync(string bucketName, string folderPath, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task CreateFolderWithCredentialsAsync(string bucketName, string folderPath, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Credentials> CreateTemporaryCredentialsAsync(string bucketName, string folderName, int durationSeconds = 3600, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task GetObjectAsync(string bucketName, string objectName, Action<Stream> callback, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, Action<Stream> callback, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Stream> GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IList<VirtualFileInfo>> ListObjectsAsync(string bucketName, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IList<VirtualFileInfo>> ListObjectsWithCredentialsAsync(string bucketName, Credentials credentials, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task PutObjectAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task PutObjectWithCredentialsAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveObjectsAsync(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveObjectsWithCredentialsAsync(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<bool> VerifyObjectExistsAsync(string bucketName, string artifactName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<Dictionary<string, bool>> VerifyObjectsExistAsync(string bucketName, IReadOnlyList<string> artifactList, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    internal class TheBadTestStorageService
    {
    }

#pragma warning restore CS8604 // Possible null reference argument.
}
