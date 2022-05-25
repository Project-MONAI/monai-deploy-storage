// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Amazon.SecurityToken.Model;
using Monai.Deploy.Storage.Common;

namespace Monai.Deploy.Storage
{
    public interface IStorageService
    {
        /// <summary>
        /// Gets or sets the name of the storage service.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Lists objects in a bucket.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="prefix">Objects with name starts with prefix</param>
        /// <param name="recursive">Whether to recurse into subdirectories</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns></returns>
        IList<VirtualFileInfo> ListObjects(string bucketName, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads an objects as stream.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="callback">Action to be called when stream is ready</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task GetObject(string bucketName, string objectName, Action<Stream> callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads an object.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="data">Stream to upload</param>
        /// <param name="size">Size of the stream</param>
        /// <param name="contentType">Content type of the object</param>
        /// <param name="metadata">Metadata of the object</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task PutObject(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies content of an object from source to destination.
        /// </summary>
        /// <param name="sourceBucketName">Name of the source bucket</param>
        /// <param name="sourceObjectName">Name of the object in the source bucket</param>
        /// <param name="destinationBucketName">Name of the destination bucket</param>
        /// <param name="destinationObjectName">Name of the object in the destination bucket</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task CopyObject(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an object.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task RemoveObject(string bucketName, string objectName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a list of objects.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectNames">An enumerable of object names to be removed in the bucket</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task RemoveObjects(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a folder with stub file.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="folderPath">Name/Path of the folder to be created. A stub file will also be created as this is required for MinIO</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task CreateFolder(string bucketName, string folderPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates temporary credentials for a specified folder for a specified length of time.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="folderName">Name/Path of the folder to be allowed access to</param>
        /// <param name="durationSeconds">Expiration time of the credentials</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task<Credentials> CreateTemporaryCredentials(string bucketName, string folderName, int durationSeconds = 3600, CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies content of an object from source to destination using temporary credentials.
        /// </summary>
        /// <param name="sourceBucketName">Name of the source bucket</param>
        /// <param name="sourceObjectName">Name of the object in the source bucket</param>
        /// <param name="destinationBucketName">Name of the destination bucket</param>
        /// <param name="destinationObjectName">Name of the object in the destination bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task CopyObjectWithCredentials(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads an objects as stream using temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="callback">Action to be called when stream is ready</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task GetObjectWithCredentials(string bucketName, string objectName, Credentials credentials, Action<Stream> callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists objects in a bucket using temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="prefix">Objects with name starts with prefix</param>
        /// <param name="recursive">Whether to recurse into subdirectories</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns></returns>
        IList<VirtualFileInfo> ListObjectsWithCredentials(string bucketName, Credentials credentials, string? prefix = "", bool recursive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads an object using temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="data">Stream to upload</param>
        /// <param name="size">Size of the stream</param>
        /// <param name="contentType">Content type of the object</param>
        /// <param name="metadata">Metadata of the object</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task PutObjectWithCredentials(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an object with temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task RemoveObjectWithCredentials(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a list of objects with temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectNames">An enumerable of object names to be removed in the bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task RemoveObjectsWithCredentials(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a folder using temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="folderPath">The path of the root folder to assign credentials to</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task CreateFolderWithCredentials(string bucketName, string folderPath, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a list of artifacts to ensure that they exist.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectDict">Artifact dictionary to verify</param>
        /// <returns>all valid artifacts</returns>
        Dictionary<string, string> VerifyObjectsExist(string bucketName, Dictionary<string, string> objectDict);

        /// <summary>
        /// Verifies the existance of an artifact to ensure that they exist.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectPair">Artifact to verify</param>
        /// <returns>valid artifact</returns>
        KeyValuePair<string, string> VerifyObjectExists(string bucketName, KeyValuePair<string, string> objectPair);
    }
}
