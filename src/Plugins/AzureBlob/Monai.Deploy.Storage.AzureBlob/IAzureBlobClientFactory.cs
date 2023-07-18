using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace Monai.Deploy.Storage.AzureBlob
{
    public interface IAzureBlobClientFactory
    {
        BlockBlobClient GetBlobBlockClient(BlobContainerClient containerClient, string blob);
        BlockBlobClient GetBlobBlockClient(string containerName, string blob);
        BlobClient GetBlobClient(BlobContainerClient containerClient, string blob);
        BlobClient GetBlobClient(string containerName, string blob);
        BlobContainerClient GetBlobContainerClient(string containerName);
        BlobServiceClient GetBlobServiceClient();
    }
}
