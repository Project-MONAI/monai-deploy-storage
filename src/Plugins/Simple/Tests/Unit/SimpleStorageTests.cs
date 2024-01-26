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

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.Configuration;
using Monai.Deploy.Storage.SimpleStorage.Exceptions;
using Moq;

namespace Monai.Deploy.Storage.SimpleStorage.Tests.Unit
{
    public class SimpleStorageTests
    {
        private readonly Mock<IFileSystem> _mokFileSystemMock = new();
        private readonly MockFileSystem _fileSystemMock;
        private readonly SimpleStorageService _simpleStorage;
        private readonly byte[] _testdataStr;
        private readonly MemoryStream _writenMemoryStream;
        private readonly Mock<IHashCreator> _hashCreatorMock = new();
        private readonly StorageServiceConfiguration _options;
        private readonly string _rootPath = "/";
        private readonly MockFileStream _mockFileStream;
        private const string MetaDataFolder = "-meta";
        private readonly string _md5Extension;
        private readonly string _metaExtension;
        private readonly string _copyExtension;

        public SimpleStorageTests()
        {
            _md5Extension = Path.Combine(MetaDataFolder, "md5");
            _metaExtension = Path.Combine(MetaDataFolder, "meta");
            _copyExtension = Path.Combine(MetaDataFolder, "copy");

            _options = new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { ConfigurationKeys.Rootpath, _rootPath } } };

            _hashCreatorMock.Setup(x => x.GetMd5Hash(It.IsAny<Stream>())).ReturnsAsync("test-hash");
            _fileSystemMock = new MockFileSystem();
            var testString = "{\"text\" : \"this is some file data\"}";
            _testdataStr = Encoding.ASCII.GetBytes(testString);
            _writenMemoryStream = new MemoryStream(_testdataStr);

            var fileData = new MockFileData(_testdataStr);

            //_fileSystemMock.AddFile("C:\\test-bucket\\test-object", fileData);
            //_fileSystemMock.AddFile("test-bucket\\test-object", fileData);
            _mokFileSystemMock.Setup(fs => fs.Path.Exists(It.IsAny<string>())).Returns(true);
            _mokFileSystemMock.Setup(fs => fs.File.ReadAllText(It.IsAny<string>())).Returns(testString);
            _mokFileSystemMock.Setup(fs => fs.Directory.CreateDirectory(It.IsAny<string>())).Returns((System.IO.Abstractions.DirectoryInfoBase)new DirectoryInfo("/"));

            _mockFileStream = new MockFileStream(_fileSystemMock, "/", FileMode.OpenOrCreate);
            _mockFileStream.Write(_testdataStr, 0, _testdataStr.Length);
            var mockFileStream2 = new MockFileStream(_fileSystemMock, "/", FileMode.OpenOrCreate);
            _mokFileSystemMock.Setup(fs => fs.File.Create(It.IsAny<string>())).Returns(_mockFileStream);
            _mokFileSystemMock.Setup(fs => fs.File.OpenRead(It.IsAny<string>())).Returns(mockFileStream2);

            _simpleStorage = new SimpleStorageService(_mokFileSystemMock.Object, _hashCreatorMock.Object, Options.Create(_options), NullLogger<SimpleStorageService>.Instance);
        }

        [Fact]
        public async Task PutObjectAsync_ShouldCreateDirectoryAndWriteFiles()
        {
            // Arrange
            var bucketName = "test-bucket";
            var objectName = "test-object";

            var size = _writenMemoryStream.Length;
            _writenMemoryStream.Seek(0, SeekOrigin.Begin);

            var contentType = "application/octet-stream";
            var metadata = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            };
            var cancellationToken = CancellationToken.None;

            var expectedPath = Path.Combine(_rootPath, bucketName, objectName);
            var expectedMd5File = Path.Combine(_rootPath, bucketName, objectName + _md5Extension);
            var expectedMetaFile = Path.Combine(_rootPath, bucketName, objectName + _metaExtension);

            var mockFileStream = new MockFileStream(_fileSystemMock, "/", FileMode.OpenOrCreate);
            _mokFileSystemMock.Setup(fs => fs.File.Create(Path.Combine(_rootPath, bucketName, objectName))).Returns(mockFileStream);

            // Act
            await _simpleStorage.PutObjectAsync(bucketName, objectName, _writenMemoryStream, size, contentType, metadata, cancellationToken).ConfigureAwait(false);

            var allFiles = _fileSystemMock.AllFiles.ToArray();
            // Assert

            _mokFileSystemMock.Verify(fs => fs.File.WriteAllTextAsync(expectedMd5File, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mokFileSystemMock.Verify(fs => fs.File.WriteAllTextAsync(expectedMetaFile, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mokFileSystemMock.Verify(fs => fs.File.Create(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CopyObjectAsync_ShouldCopyObjectToDestinationBucket()
        {
            // Arrange

            var sourceBucketName = "test-bucket";
            var sourceObjectName = "test-objec";
            var destinationBucketName = "destinationtest-bucket";
            var destinationObjectName = "destinationtest-object";  ///destinationtest-bucket\destinationtest-object

            var sourcePath = Path.Combine(sourceBucketName, sourceObjectName);
            var destinationMeataFilename = Path.Combine(_rootPath, destinationBucketName, destinationObjectName + _metaExtension);

            var metadata = new Dictionary<string, string>();

            var mockFileStream = new MockFileStream(_fileSystemMock, "/", FileMode.OpenOrCreate);
            _mokFileSystemMock.Setup(fs => fs.File.Create(Path.Combine(_rootPath, destinationBucketName, destinationObjectName))).Returns(mockFileStream);

            // Act
            await _simpleStorage.CopyObjectAsync(_writenMemoryStream, destinationBucketName, destinationObjectName).ConfigureAwait(false);

            // Assert

            _mokFileSystemMock.Verify(fs => fs.Path.Exists(destinationMeataFilename), Times.Once);
            _mokFileSystemMock.Verify(fs => fs.File.ReadAllText(destinationMeataFilename), Times.Once);
            _mokFileSystemMock.Verify(fs => fs.Directory.CreateDirectory(It.IsAny<string>()), Times.Once);
            _mokFileSystemMock.Verify(fs => fs.File.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _mokFileSystemMock.Verify(fs => fs.File.Create(It.IsAny<string>()), Times.Exactly(2));
            _hashCreatorMock.Verify(hc => hc.GetMd5Hash(It.IsAny<Stream>()), Times.Exactly(4));
        }

        [Fact]
        public async Task RemoveObjectAsync_ShouldRemoveObjectFromBucket()
        {
            // Arrange
            var bucketName = "bucket";
            var objectName = "object";
            var path = Path.Combine(_rootPath, bucketName, objectName);
            var pathmd5 = Path.Combine(_rootPath, bucketName, objectName + _md5Extension);
            var pathmeta = Path.Combine(_rootPath, bucketName, objectName + _metaExtension);
            var pathcopy = Path.Combine(_rootPath, bucketName, objectName + _copyExtension);

            _mokFileSystemMock.Setup(fs => fs.File.Exists(It.IsAny<string>())).Returns(true);

            // Act
            await _simpleStorage.RemoveObjectAsync(bucketName, objectName).ConfigureAwait(false);

            // Assert
            _mokFileSystemMock.Verify(fs => fs.File.Delete(path), Times.Once);
            _mokFileSystemMock.Verify(fs => fs.File.Delete(pathmd5), Times.Once);
            _mokFileSystemMock.Verify(fs => fs.File.Delete(pathmeta), Times.Once);
            _mokFileSystemMock.Verify(fs => fs.File.Delete(pathcopy), Times.Once);
        }

        [Fact]
        public async Task RemoveObjectsAsync_ShouldRemoveObjectsFromBucket()
        {
            // Arrange
            var bucketName = "bucket";
            IEnumerable<string> objectNames = new List<string> { "object1", "object2", "object3" };

            _mokFileSystemMock.Setup(fs => fs.File.Exists(It.IsAny<string>())).Returns(true);

            foreach (var objectName in objectNames)
            {
                var path = Path.Combine(_rootPath, bucketName, objectName);
                _mokFileSystemMock.Setup(fs => fs.File.Delete(path));
            }

            // Act
            await _simpleStorage.RemoveObjectsAsync(bucketName, objectNames).ConfigureAwait(false);

            // Assert
            foreach (var objectName in objectNames)
            {
                var path = Path.Combine(_rootPath, bucketName, objectName);
                _mokFileSystemMock.Verify(fs => fs.File.Delete(path), Times.Once);
                var pathmd5 = Path.Combine(_rootPath, bucketName, objectName + _md5Extension);
                _mokFileSystemMock.Verify(fs => fs.File.Delete(pathmd5), Times.Once);
                var pathmeta = Path.Combine(_rootPath, bucketName, objectName + _metaExtension);
                _mokFileSystemMock.Verify(fs => fs.File.Delete(pathmeta), Times.Once);
                var pathcopy = Path.Combine(_rootPath, bucketName, objectName + _copyExtension);
                _mokFileSystemMock.Verify(fs => fs.File.Delete(pathcopy), Times.Once);
            }
        }

        [Fact]
        public async Task VerifyObjectExistsAsync_ShouldReturnTrue_WhenObjectExists()
        {
            // Arrange
            var bucketName = "bucket";
            var objectName = "object";
            var path = Path.Combine(_rootPath, bucketName, objectName);

            _mokFileSystemMock.Setup(fs => fs.File.Exists(path)).Returns(true);

            // Act
            var result = await _simpleStorage.VerifyObjectExistsAsync(bucketName, objectName).ConfigureAwait(false);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task VerifyObjectExistsAsync_ShouldReturnFalse_WhenObjectDoesNotExist()
        {
            // Arrange
            var bucketName = "bucket";
            var objectName = "object";
            var path = Path.Combine(_rootPath, bucketName, objectName);

            _mokFileSystemMock.Setup(fs => fs.File.Exists(path)).Returns(false);

            // Act
            var result = await _simpleStorage.VerifyObjectExistsAsync(bucketName, objectName).ConfigureAwait(false);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifyObjectsExistAsync_ShouldReturnDictionaryWithObjectExistenceStatus()
        {
            // Arrange
            var bucketName = "bucket";
            IReadOnlyList<string> objectList = new List<string> { "object1", "object2", "object3" };

            foreach (var objectName in objectList)
            {
                var path = Path.Combine(_rootPath, bucketName, objectName);
                _mokFileSystemMock.Setup(fs => fs.File.Exists(path)).Returns(true);
            }

            // Act
            var result = await _simpleStorage.VerifyObjectsExistAsync(bucketName, objectList).ConfigureAwait(false);

            // Assert
            Assert.Equal(objectList.Count, result.Count);
            foreach (var objectName in objectList)
            {
                Assert.True(result[objectName]);
            }
        }

        [Fact]
        public async Task ListObjectsAsync_ShouldReturnListOfVirtualFileInfo()
        {
            // Arrange
            var bucketName = "test-bucket";
            var prefix = "test-prefix";
            var recursive = true;
            var filePath1 = Path.Combine(_rootPath, bucketName, "file1.txt");
            var filePath2 = Path.Combine(_rootPath, bucketName, "subfolder", "file2.txt");
            var filePath3 = Path.Combine(_rootPath, bucketName, "subfolder", "file3.txt");
            string[] filePaths = [filePath1, filePath2, filePath3];
            ulong fileSize = 100;
            var lastModified = DateTime.UtcNow;

            _mokFileSystemMock.Setup(fs => fs.Directory.GetFiles(Path.Combine(_rootPath, bucketName, prefix), "*", SearchOption.AllDirectories))
                .Returns(filePaths);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath1).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath2).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath3).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath1).LastWriteTimeUtc).Returns(lastModified);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath2).LastWriteTimeUtc).Returns(lastModified);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath3).LastWriteTimeUtc).Returns(lastModified);

            // Act
            var result = await _simpleStorage.ListObjectsAsync(bucketName, prefix, recursive, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("file1.txt", result[0].Filename);
            Assert.Equal("file1.txt", result[0].FilePath);
            Assert.Equal(fileSize, result[0].Size);
            Assert.Equal(lastModified, result[0].LastModifiedDateTime);

            Assert.Equal("file2.txt", result[1].Filename);
            Assert.Equal(Path.Combine("subfolder", "file2.txt"), result[1].FilePath);
            Assert.Equal(fileSize, result[1].Size);
            Assert.Equal(lastModified, result[1].LastModifiedDateTime);

            Assert.Equal("file3.txt", result[2].Filename);
            Assert.Equal(Path.Combine("subfolder", "file3.txt"), result[2].FilePath);
            Assert.Equal(fileSize, result[2].Size);
            Assert.Equal(lastModified, result[2].LastModifiedDateTime);
        }

        [Fact]
        public async Task ListObjectsAsync_ShouldReturnListOfVirtualFileInfo_ShouldPass_TopDirectoryOnly()
        {
            // Arrange
            var bucketName = "test-bucket";
            var prefix = "test-prefix";
            var recursive = false;
            var filePath1 = Path.Combine(_rootPath, bucketName, "file1.txt");
            var filePath2 = Path.Combine(_rootPath, bucketName, "subfolder", "file2.txt");
            var filePath3 = Path.Combine(_rootPath, bucketName, "subfolder", "file3.txt");
            string[] filePaths = [filePath1, filePath2, filePath3];
            ulong fileSize = 100;
            var lastModified = DateTime.UtcNow;

            _mokFileSystemMock.Setup(fs => fs.Directory.GetFiles(Path.Combine(_rootPath, bucketName, prefix), "*", SearchOption.TopDirectoryOnly))
                .Returns(filePaths);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath1).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath2).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath3).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath1).LastWriteTimeUtc).Returns(lastModified);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath2).LastWriteTimeUtc).Returns(lastModified);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath3).LastWriteTimeUtc).Returns(lastModified);

            // Act
            var result = await _simpleStorage.ListObjectsAsync(bucketName, prefix, recursive, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("file1.txt", result[0].Filename);
            Assert.Equal("file1.txt", result[0].FilePath);
            Assert.Equal(fileSize, result[0].Size);
            Assert.Equal(lastModified, result[0].LastModifiedDateTime);
        }

        [Fact]
        public async Task ListObjectsAsync_ShouldReturnListOfVirtualFileInfo_Not_MD5_Or_Copy_Files()
        {
            // Arrange
            var bucketName = "test-bucket";
            var prefix = "test-prefix";
            var recursive = true;
            var filePath1 = Path.Combine(_rootPath, bucketName, "file1.txt");
            var filePath2 = Path.Combine(_rootPath, bucketName, "subfolder", "file2.txt");
            var filePath3 = Path.Combine(_rootPath, bucketName, "subfolder", "file2" + MetaDataFolder, "md5");
            var filePath4 = Path.Combine(_rootPath, bucketName, "subfolder", "file2" + MetaDataFolder, "copy");
            var filePath5 = Path.Combine(_rootPath, bucketName, "subfolder", "file2" + MetaDataFolder, "meta");
            string[] filePaths = [filePath1, filePath2, filePath3, filePath4, filePath5];
            ulong fileSize = 100;
            var lastModified = DateTime.UtcNow;

            _mokFileSystemMock.Setup(fs => fs.Directory.GetFiles(Path.Combine(_rootPath, bucketName, prefix), "*", SearchOption.AllDirectories))
                .Returns(filePaths);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath1).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath2).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath3).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath4).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath5).Length).Returns((long)fileSize);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath1).LastWriteTimeUtc).Returns(lastModified);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath2).LastWriteTimeUtc).Returns(lastModified);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath3).LastWriteTimeUtc).Returns(lastModified);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath4).LastWriteTimeUtc).Returns(lastModified);
            _mokFileSystemMock.Setup(fs => fs.FileInfo.New(filePath5).LastWriteTimeUtc).Returns(lastModified);

            // Act
            var result = await _simpleStorage.ListObjectsAsync(bucketName, prefix, recursive, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("file1.txt", result[0].Filename);
            Assert.Equal("file1.txt", result[0].FilePath);
            Assert.Equal(fileSize, result[0].Size);
            Assert.Equal(lastModified, result[0].LastModifiedDateTime);

            Assert.Equal("file2.txt", result[1].Filename);
            Assert.Equal(Path.Combine("subfolder", "file2.txt"), result[1].FilePath);
            Assert.Equal(fileSize, result[1].Size);
            Assert.Equal(lastModified, result[1].LastModifiedDateTime);
        }
        [Fact]
        public async Task GetObjectAsync_ValidFile_ReturnsFileStream()
        {
            // Arrange
            var bucketName = "testBucket";
            var objectName = "testObject";
            var rootPath = "/";
            var filePath = Path.Combine(rootPath, bucketName, objectName);
            var md5path = Path.Combine(rootPath, bucketName, $"{objectName}{_md5Extension}");
            var md5Checksum = "md5Checksum";

            var mockFileStream = new MockFileStream(_fileSystemMock, "/", FileMode.OpenOrCreate);
            mockFileStream.Write(_testdataStr, 0, _testdataStr.Length);
            var mockFileStream2 = new MockFileStream(_fileSystemMock, "/", FileMode.OpenOrCreate);
            //_mokFileSystemMock.Setup(fs => fs.File.Create(It.IsAny<string>())).Returns(mockFileStream);
            _mokFileSystemMock.SetupSequence(fs => fs.File.OpenRead("/testBucket\\testObject")).Returns(mockFileStream).Returns(mockFileStream2);

            _hashCreatorMock.Setup(x => x.GetMd5Hash(It.IsAny<Stream>())).ReturnsAsync(md5Checksum);
            _mokFileSystemMock.Setup(f => f.File.Exists(filePath)).Returns(true);
            _mokFileSystemMock.Setup(f => f.File.ReadAllText(Path.Combine(rootPath, bucketName, $"{objectName}{_md5Extension}"))).Returns(md5Checksum);
            _mokFileSystemMock.Setup(f => f.File.Exists(md5path)).Returns(true);

            // Act
            var result = await _simpleStorage.GetObjectAsync(bucketName, objectName, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task GetObjectAsync_CorruptedFileAndBackup_ThrowsException()
        {
            // Arrange
            var bucketName = "testBucket";
            var objectName = "testObject";
            var rootPath = "/";
            var filePath = Path.Combine(rootPath, bucketName, objectName);
            var copyPath = Path.Combine(rootPath, bucketName, $"{objectName}{_copyExtension}");
            var md5path = Path.Combine(rootPath, bucketName, $"{objectName}{_md5Extension}");
            var md5Checksum = "md5Checksum";

            _hashCreatorMock.Setup(x => x.GetMd5Hash(It.IsAny<Stream>())).ReturnsAsync($"{md5Checksum}not");
            _mokFileSystemMock.Setup(f => f.File.Exists(filePath)).Returns(false);
            _mokFileSystemMock.Setup(f => f.File.Exists(copyPath)).Returns(false);
            _mokFileSystemMock.Setup(f => f.File.ReadAllText(Path.Combine(rootPath, bucketName, $"{objectName}.md5"))).Returns(md5Checksum);
            _mokFileSystemMock.Setup(f => f.File.Exists(md5path)).Returns(true);

            // Act & Assert
            await Assert.ThrowsAsync<FileCorruptException>(() => _simpleStorage.GetObjectAsync(bucketName, objectName, CancellationToken.None)).ConfigureAwait(false);
        }

        [Fact]
        public async Task RealFileReadWrite()
        {

            var options = new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { ConfigurationKeys.Rootpath, "./testdata" } } };
            var simpleStorage = new SimpleStorageService(new FileSystem(), new HashCreator(), Options.Create(options), NullLogger<SimpleStorageService>.Instance);

            await simpleStorage.PutObjectAsync("test-bucket", "test-object", _writenMemoryStream, _writenMemoryStream.Length, "application/octet-stream", [], CancellationToken.None).ConfigureAwait(false);

            await simpleStorage.CopyObjectAsync(_writenMemoryStream, "test-bucket", "other/destinationtest-object").ConfigureAwait(false);


            var result = await simpleStorage.ListObjectsAsync("test-bucket", "", true, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(2, result.Count);

            //mess with first file
            var oriTestString = "{\"text\" : \"this is some file data\"}";
            var testString = "{\"text\" : \"this is some Not file data\"}";
            var path = Path.Combine("./testdata", "test-bucket", "test-object");
            await File.WriteAllTextAsync(path, testString).ConfigureAwait(false);

            using var copyStream = await simpleStorage.GetObjectAsync("test-bucket", "test-object", CancellationToken.None).ConfigureAwait(false);
            await simpleStorage.CopyObjectAsync(copyStream, "destinationtest-bucket", "destinationtest-object").ConfigureAwait(false);
            copyStream.Seek(0, SeekOrigin.Begin);
            var memoryStream = new MemoryStream();
            copyStream.CopyTo(memoryStream);
            copyStream.Close();

            var text = Encoding.UTF8.GetString(memoryStream.ToArray());

            Assert.Equal(oriTestString, text);
            Assert.NotEqual(testString, text);

            await simpleStorage.RemoveObjectAsync("test-bucket", "test-object").ConfigureAwait(false);

            await simpleStorage.RemoveObjectAsync("destinationtest-bucket", "destinationtest-object").ConfigureAwait(false);

            var metaPath = Path.Combine("./testdata", "test-bucket", "test-object-meta");
            Assert.Throws<DirectoryNotFoundException>(() => { Directory.GetFiles(metaPath, "*", SearchOption.AllDirectories); });

            //should remove now empty folder
            Assert.False(await simpleStorage.VerifyObjectExistsAsync("test-bucket", "test-object-meta").ConfigureAwait(false));
        }

        [Fact]
        public async Task Verify_Should_Include_Directories_Real_Test()
        {
            var options = new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { ConfigurationKeys.Rootpath, "./testdata" } } };
            var simpleStorage = new SimpleStorageService(new FileSystem(), new HashCreator(), Options.Create(options), NullLogger<SimpleStorageService>.Instance);

            await simpleStorage.PutObjectAsync("test-bucket", "test-object", _writenMemoryStream, _writenMemoryStream.Length, "application/octet-stream", [], CancellationToken.None).ConfigureAwait(false);

            var vResults = await simpleStorage.VerifyObjectExistsAsync("test-bucket", "").ConfigureAwait(false);

            Assert.True(vResults);

            await simpleStorage.RemoveObjectAsync("test-bucket", "").ConfigureAwait(false);

            Assert.Throws<DirectoryNotFoundException>(() => { Directory.GetFiles(Path.Combine("./testdata", "test-bucket"), "*", SearchOption.AllDirectories); });

        }

        [Fact]
        public async Task List_Should_Use_Prefix_as_Directorie_Real_Test()
        {
            var options = new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { ConfigurationKeys.Rootpath, "./testdata" } } };
            var simpleStorage = new SimpleStorageService(new FileSystem(), new HashCreator(), Options.Create(options), NullLogger<SimpleStorageService>.Instance);

            await simpleStorage.PutObjectAsync("test-bucket", "test-object\\folder\\nextfolder", _writenMemoryStream, _writenMemoryStream.Length, "application/octet-stream", [], CancellationToken.None).ConfigureAwait(false);

            var vResults = await simpleStorage.ListObjectsAsync("test-bucket", "test-object\\folder", true).ConfigureAwait(false);

            Assert.True(vResults.Any());

            await simpleStorage.RemoveObjectAsync("test-bucket", "").ConfigureAwait(false);

            Assert.Throws<DirectoryNotFoundException>(() => { Directory.GetFiles(Path.Combine("./testdata", "test-bucket"), "*", SearchOption.AllDirectories); });

            options = new StorageServiceConfiguration { Settings = new Dictionary<string, string> { { ConfigurationKeys.Rootpath, "C:/temp/monaidata" } } };
            simpleStorage = new SimpleStorageService(new FileSystem(), new HashCreator(), Options.Create(options), NullLogger<SimpleStorageService>.Instance);

            var file = "8d319a11-e6a1-4513-96a4-0ff395db999d/dcm\\2.25.119983757383436971267161663925584902185\\2.25.63418180833487998694904886171176363739\\2.25.162984673648280847516503991701032289095.dcm";
            var steram = await simpleStorage.GetObjectAsync("monaideploy", file).ConfigureAwait(false);

            Assert.NotNull(steram);


        }
    }
}
