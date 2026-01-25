using Cherry.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cherry.Api.Controllers
{
    [ApiController]
    [Route("api/v1/assets")]
    public class AssetsController : ControllerBase
    {
        private readonly IStorageService _storageService;

        public AssetsController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string purpose = "GENERAL")
        {
            if (file == null || file.Length == 0) return BadRequest("File is empty");

            using (var stream = file.OpenReadStream())
            {
                var storageKey = await _storageService.SaveFileAsync(stream, file.FileName, purpose);
                return Ok(new { AssetId = storageKey, Url = await _storageService.GetPresignedUrlAsync(storageKey, TimeSpan.FromHours(1)) });
            }
        }

        [HttpGet("download/{*storageKey}")]
        public async Task<IActionResult> Download(string storageKey)
        {
            try
            {
                var stream = await _storageService.GetFileAsync(storageKey);
                var contentType = "application/octet-stream";
                if (storageKey.EndsWith(".png")) contentType = "image/png";
                else if (storageKey.EndsWith(".jpg") || storageKey.EndsWith(".jpeg")) contentType = "image/jpeg";
                else if (storageKey.EndsWith(".pptx")) contentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                else if (storageKey.EndsWith(".pdf")) contentType = "application/pdf";

                return File(stream, contentType, Path.GetFileName(storageKey));
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }
    }
}
