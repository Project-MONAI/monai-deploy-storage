<!--
  ~ Copyright 2021-2025 MONAI Consortium
  ~
  ~ Licensed under the Apache License, Version 2.0 (the "License");
  ~ you may not use this file except in compliance with the License.
  ~ You may obtain a copy of the License at
  ~
  ~     http://www.apache.org/licenses/LICENSE-2.0
  ~
  ~ Unless required by applicable law or agreed to in writing, software
  ~ distributed under the License is distributed on an "AS IS" BASIS,
  ~ WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  ~ See the License for the specific language governing permissions and
  ~ limitations under the License.
-->

# Amazon S3 for MONAI Deploy

## Overview

The AWS S3 plug-in for MONAI Deploy is based on the [Amazon S3](https://aws.amazon.com/s3/) service.

## Configuration

The `serviceAssemblyName` should be set to `Monai.Deploy.Storage.AWSS3.Awss3StorageService, Monai.Deploy.Storage.AWSS3`.

The following configurations are required to run the S3 plug-in.

| Key                  | Description                           | Sample Value                |
| -------------------- | ------------------------------------- | --------------------------- |
| accessKey            | AccessKeyId                           | key                         |
| accessToken          | SecretAccessKey                       | secret                      |
| region               | Region of the instance                | us-west-1                   |
| credentialServiceUrl | (optional) S3 crednetials service URL | sts.eu-west-1.amazonaws.com |
