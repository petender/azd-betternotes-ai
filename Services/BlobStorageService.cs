using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BetterNotes.Services
{
    public class BlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _uploadContainer;
        private readonly string _downloadContainer;
        private readonly string _accountName;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
        {
            _accountName = configuration["AzureStorage:AccountName"];
            _uploadContainer = configuration["AzureStorage:UploadContainer"] ?? "uploads";
            _downloadContainer = configuration["AzureStorage:DownloadContainer"] ?? "downloads";
            _logger = logger;

            if (string.IsNullOrEmpty(_accountName))
            {
                _logger.LogWarning("AzureStorage:AccountName is not configured. Blob storage will not be available.");
                throw new InvalidOperationException("Storage account name is not configured. Please check your app settings.");
            }

            // Use Managed Identity to connect to storage
            var blobUri = new Uri($"https://{_accountName}.blob.core.windows.net");
            
            // Use DefaultAzureCredential which handles both managed identity (in Azure) and local dev
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = false,
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeAzureCliCredential = false,
                ExcludeAzurePowerShellCredential = true,
                ExcludeInteractiveBrowserCredential = true
            });
            _logger.LogInformation("Using DefaultAzureCredential for storage authentication");
            
            _blobServiceClient = new BlobServiceClient(blobUri, credential);
            _logger.LogInformation("BlobStorageService initialized with account: {AccountName}, URI: {BlobUri}", _accountName, blobUri);
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_uploadContainer);
                
                _logger.LogInformation("Ensuring uploads container exists: {Container} in account: {AccountName}", 
                    _uploadContainer, _accountName);
                
                await containerClient.CreateIfNotExistsAsync();

                var blobName = $"{Guid.NewGuid()}_{fileName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                _logger.LogInformation("Uploading file to blob: {BlobName} in container: {Container}", 
                    blobName, _uploadContainer);
                
                await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
                {
                    ContentType = GetContentType(fileName)
                });

                _logger.LogInformation("Successfully uploaded file: {BlobName}", blobName);

                return blobName;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                _logger.LogError(ex, "Access denied (403) when uploading to uploads container. Account: {AccountName}, Container: {Container}", 
                    _accountName, _uploadContainer);
                throw new UnauthorizedAccessException($"Failed to upload to storage account '{_accountName}'. The managed identity does not have required permissions.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to blob storage. Account: {AccountName}, Container: {Container}", 
                    _accountName, _uploadContainer);
                throw;
            }
        }

        public async Task<Stream> DownloadFileAsync(string blobName, string containerName = null)
        {
            try
            {
                var container = containerName ?? _uploadContainer;
                var containerClient = _blobServiceClient.GetBlobContainerClient(container);
                var blobClient = containerClient.GetBlobClient(blobName);

                _logger.LogInformation("Downloading file from blob: {BlobName}", blobName);
                
                var response = await blobClient.DownloadAsync();
                return response.Value.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file from blob storage");
                throw;
            }
        }

        public async Task<string> UploadProcessedFileAsync(MemoryStream fileStream, string fileName)
        {
            try
            {
                _logger.LogInformation("Starting UploadProcessedFileAsync - FileName: {FileName}, Stream Length: {StreamLength}", 
                    fileName, fileStream.Length);
                
                var containerClient = _blobServiceClient.GetBlobContainerClient(_downloadContainer);
                _logger.LogInformation("Got container client for: {Container}", _downloadContainer);
                
                _logger.LogInformation("Checking if downloads container exists: {Container} in account: {AccountName}", 
                    _downloadContainer, _accountName);
                
                try
                {
                    await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                    _logger.LogInformation("CreateIfNotExistsAsync completed for container: {Container}", _downloadContainer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FAILED at CreateIfNotExistsAsync for container: {Container}", _downloadContainer);
                    throw;
                }

                var blobName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.UtcNow:yyyyMMddHHmmss}.docx";
                var blobClient = containerClient.GetBlobClient(blobName);
                _logger.LogInformation("Got blob client for: {BlobName}", blobName);

                _logger.LogInformation("Starting upload to downloads: {BlobName} in container: {Container}", 
                    blobName, _downloadContainer);
                
                fileStream.Position = 0;
                try
                {
                    await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
                    {
                        ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    });
                    _logger.LogInformation("UploadAsync completed for: {BlobName}", blobName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FAILED at UploadAsync for blob: {BlobName}", blobName);
                    throw;
                }

                _logger.LogInformation("Successfully uploaded processed file: {BlobName}", blobName);

                return blobName;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                _logger.LogError(ex, "Access denied (403) when uploading to downloads container. ErrorCode: {ErrorCode}, Message: {Message}", 
                    ex.ErrorCode, ex.Message);
                throw new UnauthorizedAccessException($"Failed to upload to storage account '{_accountName}'. The managed identity does not have required permissions. Error: {ex.ErrorCode} - {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading processed file. Type: {ExceptionType}, Account: {AccountName}, Container: {Container}, FileName: {FileName}", 
                    ex.GetType().Name, _accountName, _downloadContainer, fileName);
                throw;
            }
        }

        public async Task<(Stream stream, string contentType)> GetDownloadStreamAsync(string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_downloadContainer);
                var blobClient = containerClient.GetBlobClient(blobName);
                
                _logger.LogInformation("Attempting to download blob: {BlobName} from container: {Container} in account: {AccountName}", 
                    blobName, _downloadContainer, _accountName);
                
                var response = await blobClient.DownloadAsync();
                
                _logger.LogInformation("Successfully downloaded blob: {BlobName}, ContentType: {ContentType}", 
                    blobName, response.Value.ContentType);
                
                return (response.Value.Content, response.Value.ContentType);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                _logger.LogError(ex, "Access denied (403) when downloading blob: {BlobName}. Check that the managed identity has 'Storage Blob Data Contributor' role on the storage account.", blobName);
                throw new UnauthorizedAccessException($"Access denied to blob '{blobName}'. The managed identity may not have the required permissions. Error: {ex.Message}", ex);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogError(ex, "Blob not found (404): {BlobName} in container: {Container}", blobName, _downloadContainer);
                throw new FileNotFoundException($"Blob '{blobName}' not found in container '{_downloadContainer}'.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading blob: {BlobName} from container: {Container}", blobName, _downloadContainer);
                throw;
            }
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                ".heif" or ".heic" => "image/heif",
                _ => "application/octet-stream"
            };
        }
    }
}
