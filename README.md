<p align="center">
<img src="https://raw.githubusercontent.com/Project-MONAI/MONAI/dev/docs/images/MONAI-logo-color.png" width="50%" alt='project-monai'>
</p>

💡 If you want to know more about MONAI Deploy WG vision, overall structure, and guidelines, please read [MONAI Deploy](https://github.com/Project-MONAI/monai-deploy) first.

# MONAI Deploy Storage

[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](LICENSE)
[![codecov](https://codecov.io/gh/Project-MONAI/monai-deploy-storage/branch/master/graph/badge.svg?token=a7lu3x6kEo)](https://codecov.io/gh/Project-MONAI/monai-deploy-storage)
[![ci](https://github.com/Project-MONAI/monai-deploy-storage/actions/workflows/ci.yml/badge.svg)](https://github.com/Project-MONAI/monai-deploy-storage/actions/workflows/ci.yml)
[![Nuget](https://img.shields.io/nuget/dt/Monai.Deploy.Storage?label=NuGet%20Download)](https://www.nuget.org/packages/Monai.Deploy.Storage/)

The MONAI Deploy Storage library for MONAI Deploy clinical data pipelines system enables users to extend the system to external storage services by implementing the [IStorageService API](src/Storage/API/IStorageService.cs), which allows the users to plug in any other storage services, such as [AWS S3](https://aws.amazon.com/pm/serv-s3/) and [Azure Blob Storage](https://azure.microsoft.com/en-us/services/storage/blobs/).

Currently supported storage services:

- [MinIO](./src/Plugins//MinIO/)*
- [Amazon S3](./src/Plugins/AWSS3/)

\* Services provided may not be free or requires special license agreements. Please refer to the service providers' website for additional terms and conditions.

If you would like to use a storage service provider not listed above, please file an [issue](https://github.com/Project-MONAI/monai-deploy-storage/issues) and contribute to the repository.

---

## Installation

### 1. Configure the Service
To use the MONAI Deploy Storage library, install the [NuGet.Org](https://www.nuget.org/packages/Monai.Deploy.Storage/) package and call the `AddMonaiDeployStorageService(...)` method to register the dependencies:

```csharp

Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        ...
        services.AddMonaiDeployStorageService(hostContext.Configuration.GetSection("InformaticsGateway:storage:serviceAssemblyName").Value);
        ...
    });
```

### 2. Install the Plug-in

1. Create a subdirectory named `plug-ins` in the directory where your main application is installed.
2. Download the zipped plug-in of your choice and extract the files to the `plug-ins` directory.
3. Update `appsettings.json` and set the `serviceAssemblyName`, e.g.:  
   ```json
    "storage": {
      "serviceAssemblyName": "Monai.Deploy.Storage.MinIo.MinIoStorageService, Monai.Deploy.Storage.MinIO"
    }
   ```

### 3. Restrict Access to the Plug-ins Directory

To avoid tampering of the plug-ins, it is recommended to set access rights to the plug-ins directory.

---

## Releases

The MONAI Deploy Storage library is released in NuGet & zip formats. NuGet packages are available on both [NuGet.Org](https://www.nuget.org/packages/Monai.Deploy.Storage/) and [GitHub](https://github.com/Project-MONAI/monai-deploy-storage/packages/1350678). Zip files may be found in the build artifacts or the [Releases](https://github.com/Project-MONAI/monai-deploy-storage/releases) section.

### Official Builds

Official releases are built and released from the `main` branch.

### RC Builds

Release candidates are built and released from the `release/*` branches.

### Development Builds

Development builds are made from all branches except the `main` branch and the `release/*` branches. The NuGet packages are released to [GitHub](https://github.com/Project-MONAI/monai-deploy-storage/packages/1350678) only.

## Contributing

For guidance on contributing to MONAI Deploy Workflow Manager, see the [contributing guidelines](https://github.com/Project-MONAI/monai-deploy/blob/main/CONTRIBUTING.md).

### Writing Your Plug-in

To extend MONAI Deploy with your custom storage service provider, you must implement the [IStorageService](./src/Storage/API/IStorageService.cs) interface and extend the [ServiceRegistrationBase](./src/Storage/ServiceRegistrationBase.cs) base class.

* The **IStorageService** interface provides a set of methods required to interact with the storage layer.
* The **ServiceRegistrationBase** base class provides an abstract method _Configure()_ to configure service dependencies based on [.NET Dependency injection](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection). The derived instance is dynamically activated during runtime based on the *ServiceAssemblyName* value defined in the [StorageServiceConfiguration](./src/Storage/Configuration/StorageServiceConfiguration.cs).

## Community

To participate, please join the MONAI Deploy Workflow Manager weekly meetings on the [calendar](https://calendar.google.com/calendar/u/0/embed?src=c_954820qfk2pdbge9ofnj5pnt0g@group.calendar.google.com&ctz=America/New_York) and review the [meeting notes](https://docs.google.com/document/d/1ipCGxlq0Pd7Xnil2zGa1va99K7VbdhwcJiqel9aWzyA/edit?usp=sharing).

Join the conversation on Twitter [@ProjectMONAI](https://twitter.com/ProjectMONAI) or join our [Slack channel](https://forms.gle/QTxJq3hFictp31UM9).

Ask and answer questions over on [MONAI Deploy Storage's GitHub Discussions tab](https://github.com/Project-MONAI/monai-deploy-storage/discussions).

## License

Copyright (c) MONAI Consortium. All rights reserved.
Licensed under the [Apache-2.0](LICENSE) license.

This software uses the Microsoft .NET 6.0 library, and the use of this software is subject to the [Microsoft software license terms](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm).

By downloading this software, you agree to the license terms & all licenses listed on the [third-party licenses](third-party-licenses.md) page.

## Links

- Website: <https://monai.io>
- Code: <https://github.com/Project-MONAI/monai-deploy-storage>
- Project tracker: <https://github.com/Project-MONAI/monai-deploy-storage/projects>
- Issue tracker: <https://github.com/Project-MONAI/monai-deploy-storage/issues>
- Test status: <https://github.com/Project-MONAI/monai-deploy-storage/actions>
