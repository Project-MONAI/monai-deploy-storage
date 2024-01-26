/*
 * Copyright 2023 MONAI Consortium
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.API;
using Monai.Deploy.Storage.Configuration;
using Monai.Deploy.Storage.SimpleStorage.Exceptions;
using Polly;
using Polly.Retry;
using System.IO.Abstractions;
using System.Text.Json;
using FileNotFoundException = Monai.Deploy.Storage.SimpleStorage.Exceptions.FileNotFoundException;

namespace Monai.Deploy.Storage.SimpleStorage
{

    public class SimpleStorageService : IStorageService
    {
        private readonly IFileSystem _fileSystem;

        public string Name => throw new NotImplementedException();
        private ResiliencePipeline Polly { get; set; }
        private readonly IHashCreator _hashCreator;
        private readonly ILogger<SimpleStorageService> _logger;
        private readonly StorageServiceConfiguration _configuration;
        private readonly string _rootpath;
        private const string MetaDataFolder = "-meta";
        private readonly string _md5Extension;
        private readonly string _metaExtension;
        private readonly string _copyExtension;

        public SimpleStorageService(IFileSystem fileSystem, IHashCreator hashCreator, IOptions<StorageServiceConfiguration> options, ILogger<SimpleStorageService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hashCreator = hashCreator ?? throw new ArgumentNullException(nameof(hashCreator));
            _configuration = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _rootpath = _configuration.Settings[ConfigurationKeys.Rootpath];

            _md5Extension = Path.Combine(MetaDataFolder, "md5");
            _metaExtension = Path.Combine(MetaDataFolder, "meta");
            _copyExtension = Path.Combine(MetaDataFolder, "copy");

            Polly = new ResiliencePipelineBuilder()
           .AddRetry(new RetryStrategyOptions
           {
               MaxRetryAttempts = 3,
               Delay = TimeSpan.FromSeconds(0),
               OnRetry = args =>
               {
                   _logger.FileWriteErrorRetrying(args.AttemptNumber, args.Outcome.Exception?.Message);
                   return default;
               }
           })
           .Build();
        }

        private FileSystemStream GetFileStream(string path)
        {
            if (_fileSystem.File.Exists(path))
            {
                return _fileSystem.File.OpenRead(path);
            }
            throw new FileNotFoundException($"File {path} not found");
        }

        public async Task CopyObjectAsync(
            string sourceBucketName,
            string sourceObjectName,
            string destinationBucketName,
            string destinationObjectName,
            CancellationToken cancellationToken = default)
        {
            var sourcePath = Path.Combine(_rootpath, sourceBucketName, sourceBucketName);

            using var sourceStream = GetFileStream(sourcePath);

            await CopyObjectAsync(
                sourceStream,
                destinationBucketName,
                destinationObjectName,
                cancellationToken).ConfigureAwait(false);

            sourceStream.Close();
        }

        internal Task CopyObjectAsync(
            Stream sourceStream,
            string destinationBucketName,
            string destinationObjectName,
            CancellationToken cancellationToken = default)
        {

            var metadata = new Dictionary<string, string>();
            var destinationMetaFilename = Path.Combine(_rootpath, destinationBucketName, $"{destinationObjectName}{_metaExtension}");

            if (_fileSystem.Path.Exists(destinationMetaFilename))
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(_fileSystem.File.ReadAllText(destinationMetaFilename)) ?? [];
            }

            return PutObjectAsync(
                destinationBucketName,
                destinationObjectName,
                sourceStream,
                sourceStream.Length,
                "application/octet-stream",
                metadata,
                cancellationToken);
        }

        public Task CopyObjectWithCredentialsAsync(
            string sourceBucketName,
            string sourceObjectName,
            string destinationBucketName,
            string destinationObjectName,
            Credentials credentials,
            CancellationToken cancellationToken = default)
        {
            return CopyObjectAsync(sourceBucketName, sourceObjectName, destinationBucketName, destinationObjectName, cancellationToken);
        }
        public Task CreateFolderAsync(string bucketName, string folderPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_fileSystem.Directory.CreateDirectory(Path.Combine(_rootpath, bucketName, folderPath)));
        }

        public Task CreateFolderWithCredentialsAsync(string bucketName, string folderPath, Credentials credentials, CancellationToken cancellationToken = default)
        {
            return CreateFolderAsync(bucketName, folderPath, cancellationToken);
        }

        public Task<Credentials> CreateTemporaryCredentialsAsync(string bucketName, string folderName, int durationSeconds = 3600, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Credentials());
        }

        public async Task<Stream> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            if (objectName.EndsWith(_metaExtension))
            {
                return await GetObjectMetaAsync(bucketName, objectName).ConfigureAwait(false);
            }

            return await GetObjectMainAsync(bucketName, objectName, cancellationToken).ConfigureAwait(false);

        }

        private async Task<Stream> GetObjectMainAsync(string bucketName, string objectName, CancellationToken cancellationToken)
        {
            var md5path = Path.Combine(_rootpath, bucketName, $"{objectName}{_md5Extension}");
            if (await VerifyObjectExistsAsync(bucketName, $"{objectName}{_md5Extension}", cancellationToken).ConfigureAwait(false) is false)
            {
                throw new FileNotFoundException($"File {objectName} not found in bucket {bucketName}");
            }
            var path = Path.Combine(_rootpath, bucketName, objectName);
            var checksum = _fileSystem.File.ReadAllText(md5path);

            if (await CheckFileAsync(path, checksum).ConfigureAwait(false))
            {
                var returningStream = new MemoryStream();
                var st = _fileSystem.File.OpenRead(path);
                await st.CopyToAsync(returningStream, cancellationToken).ConfigureAwait(false);
                return returningStream;
            }
            var pathCopy = Path.Combine(_rootpath, bucketName, $"{objectName}{_copyExtension}");
            if (await CheckFileAsync(pathCopy, checksum).ConfigureAwait(false))
            {
                _logger.MainFileCorrupt(path, pathCopy);

                _fileSystem.File.Copy(pathCopy, path, true);
                return _fileSystem.File.OpenRead(pathCopy);
            }
            _logger.MainFileCorrupt(path, pathCopy);
            throw new FileCorruptException("File and backup is corrupted");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<Stream> GetObjectMetaAsync(string bucketName, string objectName)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var path = Path.Combine(_rootpath, bucketName, $"{objectName}{_metaExtension}");
            if (_fileSystem.File.Exists(path))
            {
                var returnMemoryStream = new MemoryStream();
                await _fileSystem.File.OpenRead(path).CopyToAsync(returnMemoryStream).ConfigureAwait(false);
                return returnMemoryStream;
            }
            throw new FileNotFoundException($"File {objectName} not found in bucket {bucketName}");
        }

        public Task<Stream> GetObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            return GetObjectAsync(bucketName, objectName, cancellationToken);
        }

        public Task<IList<VirtualFileInfo>> ListObjectsAsync(string bucketName, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            var allfiles = _fileSystem.Directory.GetFiles(Path.Combine(_rootpath, bucketName, prefix), "*", recursive == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            var prepathLength = Path.Combine(_rootpath, bucketName).Length + 1;

            var result = allfiles
                .Where(x => x.EndsWith(_md5Extension) is false && x.EndsWith(_copyExtension) is false && x.EndsWith(_metaExtension) is false)
                .Select(x => new VirtualFileInfo(Path.GetFileName(x), x.Substring(prepathLength), ReadFileMd5Async(x), (ulong)_fileSystem.FileInfo.New(x).Length)
                {
                    LastModifiedDateTime = _fileSystem.FileInfo.New(x).LastWriteTimeUtc
                }).ToList();
            return Task.FromResult((IList<VirtualFileInfo>)result);
        }

        public Task<IList<VirtualFileInfo>> ListObjectsWithCredentialsAsync(string bucketName, Credentials credentials, string prefix = "", bool recursive = false, CancellationToken cancellationToken = default)
        {
            return ListObjectsAsync(bucketName, prefix, recursive, cancellationToken);
        }

        public async Task PutObjectAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(_rootpath, bucketName, objectName);
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.FileCannotBeEmpty(objectName, bucketName);
                throw new ArgumentNullException(nameof(objectName));
            }

            string? dirName = null;
            if ((dirName = Path.GetDirectoryName(path + _md5Extension)) is not null && Directory.Exists(dirName) is false)
            {
                _fileSystem.Directory.CreateDirectory(dirName);
            }

            var Md5Hash = await WriteFile(path, data, cancellationToken).ConfigureAwait(false);

            var md5File = Path.Combine(_rootpath, bucketName, $"{objectName}{_md5Extension}");

            List<Task> Tasks = new();

            Tasks.Add(_fileSystem.File.WriteAllTextAsync(md5File, Md5Hash, cancellationToken));


            var metaFile = Path.Combine(_rootpath, bucketName, $"{objectName}{_metaExtension}");
            Tasks.Add(_fileSystem.File.WriteAllTextAsync(metaFile, JsonSerializer.Serialize(metadata), cancellationToken));

            var copyFile = Path.Combine(_rootpath, bucketName, $"{objectName}{_copyExtension}");
            Tasks.Add(WriteFile(copyFile, data, cancellationToken));

            await Task.WhenAll(Tasks).ConfigureAwait(false);
        }

        public Task PutObjectWithCredentialsAsync(string bucketName, string objectName, Stream data, long size, string contentType, Dictionary<string, string> metadata, Credentials credentials, CancellationToken cancellationToken = default)
        {
            return PutObjectAsync(bucketName, objectName, data, size, contentType, metadata, cancellationToken);
        }

        public async Task RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
        {
            if (await VerifyObjectExistsAsync(bucketName, objectName, cancellationToken).ConfigureAwait(false))
            {

                if (await VerifyObjectExistsAsync(bucketName, $"{objectName}{_md5Extension}", cancellationToken).ConfigureAwait(false))
                {
                    var md5File = Path.Combine(_rootpath, bucketName, $"{objectName}{_md5Extension}");
                    _fileSystem.File.Delete(md5File);
                }

                if (await VerifyObjectExistsAsync(bucketName, $"{objectName}{_metaExtension}", cancellationToken).ConfigureAwait(false))
                {
                    var metaFile = Path.Combine(_rootpath, bucketName, $"{objectName}{_metaExtension}");
                    _fileSystem.File.Delete(metaFile);
                }

                if (await VerifyObjectExistsAsync(bucketName, $"{objectName}{_copyExtension}", cancellationToken).ConfigureAwait(false))
                {
                    var copyFile = Path.Combine(_rootpath, bucketName, $"{objectName}{_copyExtension}");
                    _fileSystem.File.Delete(copyFile);
                    _fileSystem.Directory.Delete(Path.GetDirectoryName(copyFile)!);
                }
                var path = Path.Combine(_rootpath, bucketName, objectName);

                if (_fileSystem.File.Exists(path))
                {
                    _fileSystem.File.Delete(path);
                }
                else
                {
                    _fileSystem.Directory.Delete(path, true);
                }
                _logger.RemovedFile(objectName, bucketName);
                return;
            }
            throw new FileNotFoundException($"File {objectName} not found in bucket {bucketName}");
        }

        public Task RemoveObjectsAsync(string bucketName, IEnumerable<string> objectNames, CancellationToken cancellationToken = default)
        {
            var tasks = objectNames.Select(x => RemoveObjectAsync(bucketName, x, cancellationToken));
            return Task.WhenAll(tasks);
        }

        public Task RemoveObjectsWithCredentialsAsync(string bucketName, IEnumerable<string> objectNames, Credentials credentials, CancellationToken cancellationToken = default)
        {
            return RemoveObjectsAsync(bucketName, objectNames, cancellationToken);
        }

        public Task RemoveObjectWithCredentialsAsync(string bucketName, string objectName, Credentials credentials, CancellationToken cancellationToken = default)
        {
            return RemoveObjectAsync(bucketName, objectName, cancellationToken);
        }

        public Task<bool> VerifyObjectExistsAsync(string bucketName, string artifactName, CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(_rootpath, bucketName, artifactName);
            return Task.FromResult(_fileSystem.Directory.Exists(path) || _fileSystem.File.Exists(path));
        }

        public async Task<Dictionary<string, bool>> VerifyObjectsExistAsync(string bucketName, IReadOnlyList<string> artifactList, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, bool>();
            foreach (var artifactName in artifactList)
            {
                var path = Path.Combine(_rootpath, bucketName, artifactName);
                result.Add(artifactName, (await VerifyObjectExistsAsync(bucketName, artifactName, cancellationToken).ConfigureAwait(false)));
            }
            return result;
        }


        private async Task<string> WriteFile(string path, Stream dataStream, CancellationToken cancellationToken = default)
        {
            return await Polly.ExecuteAsync(async token =>
            {
                dataStream.Seek(0, SeekOrigin.Begin);
                var destination = _fileSystem.File.Create(path);

                await dataStream.CopyToAsync(destination, token).ConfigureAwait(false);
                destination.Close();
                var Md5Hash = await _hashCreator.GetMd5Hash(dataStream).ConfigureAwait(false);

                if ((await CheckFileAsync(path, Md5Hash).ConfigureAwait(false)) is false)
                {
                    throw new Exception("File is corrupted");
                }

                return Md5Hash;
            }, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> CheckFileAsync(string path, string md5checksum)
        {
            using var stream = _fileSystem.File.OpenRead(path);
            var fileMd5 = await _hashCreator.GetMd5Hash(stream).ConfigureAwait(false);
            stream.Close();
            return fileMd5 == md5checksum;
        }

        private string ReadFileMd5Async(string path)
        {
            var md5path = $"{path}{_md5Extension}";
            if (_fileSystem.File.Exists(md5path))
            {
                return _fileSystem.File.ReadAllText(md5path);
            }
            return "";
        }
    }
}
