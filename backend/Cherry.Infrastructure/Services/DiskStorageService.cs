using Cherry.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Cherry.Infrastructure.Services
{
    public class DiskStorageService : IStorageService
    {
        private readonly string _basePath;

        public DiskStorageService(IConfiguration configuration)
        {
            _basePath = configuration["Storage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Storage");
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folder)
        {
            var folderPath = Path.Combine(_basePath, folder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var storageKey = Path.Combine(folder, $"{Guid.NewGuid()}_{fileName}");
            var fullPath = Path.Combine(_basePath, storageKey);

            using (var ws = new FileStream(fullPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(ws);
            }

            return storageKey.Replace("\\", "/");
        }

        public Task<Stream> GetFileAsync(string storageKey)
        {
            var fullPath = Path.Combine(_basePath, storageKey.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!File.Exists(fullPath)) throw new FileNotFoundException();
            return Task.FromResult<Stream>(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
        }

        public Task<string> GetPresignedUrlAsync(string storageKey, TimeSpan expiry)
        {
            // For disk storage, we return a local API endpoint URL
            return Task.FromResult($"/api/v1/assets/download/{storageKey}");
        }

        public string GetStoragePath(string storageKey)
        {
            return Path.Combine(_basePath, storageKey.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }
    }
}
