using System;
using System.IO;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.PyroAlerts.Entitys;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Media;

public class WebRootMediaFileStorageService : IMediaFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WebRootMediaFileStorageService> _logger;

    public WebRootMediaFileStorageService(IWebHostEnvironment env, ILogger<WebRootMediaFileStorageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<MediaFileInfo> SaveFileAsync(IFormFile file, string? targetRelativePathHint = null)
    {
        var uploadsRoot = Path.Combine(_env.WebRootPath, "media", "uploads");
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var fs = File.Create(fullPath))
        {
            await file.CopyToAsync(fs);
            await fs.FlushAsync();
        }

        var relativeUrl = "/" + Path.Combine("media", "uploads", fileName).Replace(Path.DirectorySeparatorChar, '/');

        var mediaType = await extension.GetFileMediaTypeAsync();

        var info = new MediaFileInfo
        {
            Type = mediaType,
            FilePath = NormalizePath(relativeUrl),
            IsLocalFile = true,
            FileName = fileName,
            Extension = extension,
        };

        if (_env.IsDevelopment())
        {
            try
            {
                await CopyToDevCopiesAsync(info.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy media file to dev copies");
            }
        }

        return info;
    }

    public Task DeleteFileAsync(string relativePath)
    {
        var full = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full))
        {
            File.Delete(full);
        }

        return Task.CompletedTask;
    }

    public Task CopyToDevCopiesAsync(string relativePath)
    {
        var trimmed = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var sourceFull = Path.Combine(_env.WebRootPath, trimmed);

        if (!File.Exists(sourceFull))
        {
            return Task.CompletedTask;
        }

        var devRoot = Path.Combine(_env.WebRootPath, "Alerts", "random_meme");
        Directory.CreateDirectory(devRoot);

        var fileName = Path.GetFileName(sourceFull);
        var destFull = Path.Combine(devRoot, fileName);

        File.Copy(sourceFull, destFull, true);

        return Task.CompletedTask;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        var single = path.Replace("//", "/");
        while (single.Contains("//")) single = single.Replace("//", "/");

        if (!single.StartsWith('/')) single = "/" + single;
        return single;
    }
}
