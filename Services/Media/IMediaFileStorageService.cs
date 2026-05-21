using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MARS.Server.Services.PyroAlerts.Entitys;

namespace MARS.Server.Services.Media;

public interface IMediaFileStorageService
{
    Task<MediaFileInfo> SaveFileAsync(IFormFile file, string? targetRelativePathHint = null);
    Task DeleteFileAsync(string relativePath);
    Task CopyToDevCopiesAsync(string relativePath);
}
