<!--
  ~ Copyright 2022 MONAI Consortium
  ~
  ~ Licensed under the Apache License, Version 2.0 (the "License");
  ~ you may not use this file except in compliance with the License.
  ~ You may obtain a copy of the License at
  ~
  ~ http://www.apache.org/licenses/LICENSE-2.0
  ~
  ~ Unless required by applicable law or agreed to in writing, software
  ~ distributed under the License is distributed on an "AS IS" BASIS,
  ~ WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  ~ See the License for the specific language governing permissions and
  ~ limitations under the License.
-->

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