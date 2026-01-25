using System;
using System.IO;
using System.Threading.Tasks;

namespace Cherry.Core.Interfaces
{
    public interface IStorageService
    {
        Task<string> SaveFileAsync(Stream fileStream, string fileName, string folder);
        Task<Stream> GetFileAsync(string storageKey);
        Task<string> GetPresignedUrlAsync(string storageKey, TimeSpan expiry);
        string GetStoragePath(string storageKey);
    }
}
