# MinIO for MONAI Deploy

## Overview

The MinIO plug-in for MONAI Deploy is based on the [MinIO](https://min.io/) solution.

## Configuration

The `serviceAssemblyName` should be set to `Monai.Deploy.Storage.MinIo.MinIoStorageService, Monai.Deploy.Storage.MinIO`.

The following configurations are required to run the MinIO plug-in.

| Key                | Description                                                | Sample Value   |
| ------------------ | ---------------------------------------------------------- | -------------- |
| endpoint           | Host name/IP and port.                                     | localhost:9000 |
| accessKey          | Access key/username                                        | username       |
| accessToken        | Secret key/password                                        | password       |
| securedConnection  | Indicates if connection should be secured or not           | true/false     |
| region             | Region of the instance                                     | local          |
| executableLocation | Path to `mc.exe`. (used by `IMinioAdmin`)                  | /path/to/mc    |
| serviceName        | Alias for the `mc.exe` connection. (used by `IMinioAdmin`) | monaiminio     |