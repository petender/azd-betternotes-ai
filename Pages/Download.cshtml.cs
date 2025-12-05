using BetterNotes.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BetterNotes.Pages
{
    public class DownloadModel : PageModel
    {
        private readonly BlobStorageService _blobStorageService;
        private readonly ILogger<DownloadModel> _logger;

        public DownloadModel(BlobStorageService blobStorageService, ILogger<DownloadModel> logger)
        {
            _blobStorageService = blobStorageService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return NotFound();
            }

            try
            {
                var (stream, contentType) = await _blobStorageService.GetDownloadStreamAsync(file);
                return File(stream, contentType ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document", file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file: {FileName}", file);
                return NotFound();
            }
        }
    }
}