// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.DependencyInjection;
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

        [Fact(DisplayName = "AddMonaiDeployStorageService throws if service registrar cannot be found in the assembly")]
        public void AddMonaiDeployStorageService_ThrowsIfServiceRegistrarCannotBeFoundInTheAssembly()
        {
            var badType = typeof(Assert);
            var typeName = badType.AssemblyQualifiedName;
            var assemblyData = GetAssemblyeBytes(badType.Assembly);
            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, badType.Assembly.ManifestModule.Name);
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);
            var serviceCollection = new Mock<IServiceCollection>();
            var exception = Assert.Throws<ConfigurationException>(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, fileSystem));

            Assert.NotNull(exception);
            Assert.Equal($"Service registrar cannot be found for the configured plug-in '{typeName}'.", exception.Message);
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
            var badType = typeof(GoodStorageService);
            var typeName = badType.AssemblyQualifiedName;
            var assemblyData = GetAssemblyeBytes(badType.Assembly);
            var assemblyFilePath = Path.Combine(SR.PlugInDirectoryPath, badType.Assembly.ManifestModule.Name);
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory(SR.PlugInDirectoryPath);
            fileSystem.File.WriteAllBytes(assemblyFilePath, assemblyData);
            var serviceCollection = new Mock<IServiceCollection>();
            serviceCollection.Setup(p => p.Clear());
            var exception = Record.Exception(() => serviceCollection.Object.AddMonaiDeployStorageService(typeName, fileSystem));

            Assert.Null(exception);

            serviceCollection.Verify(p => p.Clear(), Times.Once());
        }

        private static byte[] GetAssemblyeBytes(Assembly assembly)
        {
            return File.ReadAllBytes(assembly.Location);
        }
    }

    internal class TestServiceRegistrar : ServiceRegistrationBase
    {
        public TestServiceRegistrar(string fullyQualifiedAssemblyName) : base(fullyQualifiedAssemblyName)
        {
        }

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

        public Task GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, Action<Stream> callback, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IList<VirtualFileInfo>> ListObjectsAsync(string bucketName, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IList<VirtualFileInfo>> ListObjectsWithCredentialsAsync(string bucketName, Credentials credentials, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task PutObjectAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task PutObjectWithCredentialsAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveObjectsAsync(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveObjectsWithCredentialsAsync(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task RemoveObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<KeyValuePair<string, string>> VerifyObjectExistsAsync(string bucketName, KeyValuePair<string, string> objectPair) => throw new NotImplementedException();

        public Task<Dictionary<string, string>> VerifyObjectsExistAsync(string bucketName, Dictionary<string, string> objectDict) => throw new NotImplementedException();
    }

    internal class TheBadTestStorageService
    {
    }

#pragma warning restore CS8604 // Possible null reference argument.
}
