using BetterNotes.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BetterNotes.Pages
{
    [IgnoreAntiforgeryToken]
    public class UploadModel : PageModel
    {
        private readonly ILogger<UploadModel> _logger;
        private readonly AzureAIService _azureAIService;
        private readonly FileProcessingService _fileProcessingService;
        private readonly BlobStorageService _blobStorageService;

        public UploadModel(
            ILogger<UploadModel> logger, 
            AzureAIService azureAIService, 
            FileProcessingService fileProcessingService,
            BlobStorageService blobStorageService)
        {
            _logger = logger;
            _azureAIService = azureAIService;
            _fileProcessingService = fileProcessingService;
            _blobStorageService = blobStorageService;
        }

        [BindProperty]
        public IFormFile UploadedFile { get; set; }

        public string Message { get; set; }
        public string AnalysisResult { get; set; }
        public string DownloadLink { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("File upload initiated.");

            if (UploadedFile == null || UploadedFile.Length == 0)
            {
                _logger.LogWarning("No file uploaded or file is empty.");
                Message = "Please select a valid file.";
                return Page();
            }

            // Validate file type
            var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".heif", ".heic" };
            var fileExtension = Path.GetExtension(UploadedFile.FileName).ToLowerInvariant();
            
            if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
            {
                _logger.LogWarning("Unsupported file type uploaded: {FileExtension}", fileExtension);
                Message = $"Unsupported file type '{fileExtension}'. Please upload a PDF, DOCX, or image file (JPG, PNG, BMP, TIFF, HEIF).";
                return Page();
            }

            // Validate file size (500 MB for paid tier, but let's set a reasonable limit)
            const long maxFileSize = 100 * 1024 * 1024; // 100 MB
            if (UploadedFile.Length > maxFileSize)
            {
                _logger.LogWarning("File too large: {FileSize} bytes", UploadedFile.Length);
                Message = $"File is too large. Maximum file size is 100 MB.";
                return Page();
            }

            try
            {
                // Upload file to blob storage
                string blobName;
                using (var stream = UploadedFile.OpenReadStream())
                {
                    blobName = await _blobStorageService.UploadFileAsync(stream, UploadedFile.FileName);
                }

                _logger.LogInformation("File uploaded to blob storage: {BlobName}", blobName);
                Message = "File uploaded successfully.";

                // Download the file from blob storage for AI analysis
                using (var fileStream = await _blobStorageService.DownloadFileAsync(blobName))
                {
                    // Call Azure AI Service
                    AnalysisResult = await _azureAIService.AnalyzeFileAsync(fileStream, UploadedFile.FileName);
                    _logger.LogInformation("Analysis completed successfully");
                }

                // Create Word document with results
                var wordDocument = _fileProcessingService.CreateWordDocument(AnalysisResult, UploadedFile.FileName);
                
                // Upload processed file to downloads container
                var processedBlobName = await _blobStorageService.UploadProcessedFileAsync(wordDocument, UploadedFile.FileName);
                
                // Generate download link
                DownloadLink = $"/Download?file={Uri.EscapeDataString(processedBlobName)}";
                
                _logger.LogInformation("Processed file uploaded: {ProcessedBlobName}", processedBlobName);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Authorization error processing file: {FileName}", UploadedFile.FileName);
                Message = $"Authorization error: {ex.Message}. Please wait 5-10 minutes for role assignments to propagate and try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file: {FileName}. Exception Type: {ExceptionType}", UploadedFile.FileName, ex.GetType().Name);
                Message = $"An error occurred while processing the file: {ex.Message}";
                
                // Log inner exception if present
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
                }
            }

            return Page();
        }
    }
}