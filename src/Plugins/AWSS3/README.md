# Amazon S3 for MONAI Deploy

## Overview

The AWS S3 plug-in for MONAI Deploy is based on the [Amazon S3](https://aws.amazon.com/s3/) service.

## Configuration

The `serviceAssemblyName` should be set to `Monai.Deploy.Storage.AWSS3.Awss3StorageService, Monai.Deploy.Storage.AWSS3`.

The following configurations are required to run the MinIO plug-in.

| Key                  | Description                | Sample Value                |
| -------------------- | -------------------------- | --------------------------- |
| accessKey            | AccessKeyId                | key                         |
| accessToken          | SecretAccessKey            | secret                      |
| region               | Region of the instance     | us-west-1                   |
| credentialServiceUrl | S3 crednetials service URL | sts.eu-west-1.amazonaws.com |