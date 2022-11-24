/*
 * Copyright 2021-2022 MONAI Consortium
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

using Amazon.SecurityToken.Model;

namespace Monai.Deploy.Storage.API
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
        Task<IList<VirtualFileInfo>> ListObjectsAsync(string bucketName, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads an objects as stream.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Stream</returns>
        Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);

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
        Task PutObjectAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies content of an object from source to destination.
        /// </summary>
        /// <param name="sourceBucketName">Name of the source bucket</param>
        /// <param name="sourceObjectName">Name of the object in the source bucket</param>
        /// <param name="destinationBucketName">Name of the destination bucket</param>
        /// <param name="destinationObjectName">Name of the object in the destination bucket</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task CopyObjectAsync(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an object.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a list of objects.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectNames">An enumerable of object names to be removed in the bucket</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task RemoveObjectsAsync(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a folder with stub file.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="folderPath">Name/Path of the folder to be created. A stub file will also be created as this is required for MinIO</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task CreateFolderAsync(string bucketName, string folderPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a list of artifacts to ensure that they exist.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="artifactList">Artifacts to verify</param>
        /// <returns>all valid artifacts</returns>
        Task<Dictionary<string, bool>> VerifyObjectsExistAsync(string bucketName, IReadOnlyList<string> artifactList, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies the existence of an artifact to ensure that they exist.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="artifactName">Artifact to verify</param>
        /// <returns>valid artifact</returns>
        Task<bool> VerifyObjectExistsAsync(string bucketName, string artifactName, CancellationToken cancellationToken = default);

        #region Temporary Credential APIs

        /// <summary>
        /// Creates temporary credentials for a specified folder for a specified length of time.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="folderName">Name/Path of the folder to be allowed access to</param>
        /// <param name="durationSeconds">Expiration time of the credentials</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task<Credentials> CreateTemporaryCredentialsAsync(string bucketName, string folderName, int durationSeconds = 3600, CancellationToken cancellationToken = default);

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
        Task CopyObjectWithCredentialsAsync(string sourceBucketName, string sourceObjectName, string destinationBucketName, string destinationObjectName, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads an objects as stream using temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="callback">Action to be called when stream is ready</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task<Stream> GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists objects in a bucket using temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="prefix">Objects with name starts with prefix</param>
        /// <param name="recursive">Whether to recurse into subdirectories</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns></returns>
        Task<IList<VirtualFileInfo>> ListObjectsWithCredentialsAsync(string bucketName, Credentials credentials, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default);

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
        Task PutObjectWithCredentialsAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an object with temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object in the bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task RemoveObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a list of objects with temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectNames">An enumerable of object names to be removed in the bucket</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task RemoveObjectsWithCredentialsAsync(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a folder using temporary credentials.
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="folderPath">The path of the root folder to assign credentials to</param>
        /// <param name="credentials">Temporary credentials used to connect</param>
        /// <param name="cancellationToken">Optional cancellation token. Defaults to default(CancellationToken)</param>
        /// <returns>Task</returns>
        Task CreateFolderWithCredentialsAsync(string bucketName, string folderPath, Credentials credentials, CancellationToken cancellationToken = default);

        #endregion Temporary Credential APIs
    }
}
