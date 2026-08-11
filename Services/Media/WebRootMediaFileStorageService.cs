using MARS.Server.Exstensions;
using MARS.Server.Services.PyroAlerts.Entitys;

namespace MARS.Server.Services.Media;

public class WebRootMediaFileStorageService(
    IWebHostEnvironment env,
    ILogger<WebRootMediaFileStorageService> logger
) : IMediaFileStorageService
{
    private const string DefaultFolderName = "Alerts/uploaded_mems";

    public async Task<MediaFileInfo> SaveFileAsync(
        IFormFile file,
        string? targetRelativePathHint = null
    )
    {
        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        var relativePath = ResolveRelativePath(targetRelativePathHint, extension);
        var fullPath = Path.Combine(
            env.WebRootPath,
            relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
        );

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var fs = File.Create(fullPath))
        {
            await file.CopyToAsync(fs);
            await fs.FlushAsync();
        }

        var relativeUrl = NormalizePath(relativePath);

        var mediaType = await extension.GetFileMediaTypeAsync();
        var resolvedFileName = Path.GetFileName(fullPath);

        var info = new MediaFileInfo
        {
            Type = mediaType,
            FilePath = NormalizePath(relativeUrl),
            IsLocalFile = true,
            FileName = resolvedFileName,
            Extension = extension,
        };

        try
        {
            await CopyToDevCopiesAsync(info.FilePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to copy media file to dev copies");
        }

        return info;
    }

    public Task DeleteFileAsync(string relativePath)
    {
        var full = Path.Combine(
            env.WebRootPath,
            relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
        );
        if (File.Exists(full))
        {
            File.Delete(full);
        }

        return Task.CompletedTask;
    }

    public Task CopyToDevCopiesAsync(string relativePath)
    {
        var normalizedRelativePath = NormalizePath(relativePath);
        var sourceFull = Path.Combine(
            env.WebRootPath,
            normalizedRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
        );

        if (!File.Exists(sourceFull))
        {
            return Task.CompletedTask;
        }

        var devWebRoot = ResolveDevWebRoot();
        var devRelativePath = normalizedRelativePath
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        var destFull = Path.Combine(devWebRoot, devRelativePath);

        var destDirectory = Path.GetDirectoryName(destFull);
        if (!string.IsNullOrWhiteSpace(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }

        File.Copy(sourceFull, destFull, true);

        return Task.CompletedTask;
    }

    private static string ResolveRelativePath(
        string? targetRelativePathHint,
        string sourceExtension
    )
    {
        if (string.IsNullOrWhiteSpace(targetRelativePathHint))
        {
            var defaultFileName = $"{Guid.NewGuid()}{sourceExtension}";
            return "/"
                + Path.Combine(DefaultFolderName, defaultFileName)
                    .Replace(Path.DirectorySeparatorChar, '/');
        }

        var normalizedHint = NormalizePath(targetRelativePathHint);
        var trimmedHint = normalizedHint.TrimStart('/');

        if (Path.IsPathRooted(trimmedHint) || trimmedHint.Contains(".."))
        {
            throw new InvalidOperationException("Некорректный относительный путь для файла");
        }

        var finalPath = normalizedHint;
        if (string.IsNullOrWhiteSpace(Path.GetExtension(trimmedHint)))
        {
            finalPath = NormalizePath(normalizedHint + sourceExtension);
        }

        return finalPath;
    }

    private string ResolveDevWebRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var projectRoot = FindProjectRoot(currentDirectory);

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var candidate = Path.Combine(projectRoot, "wwwroot");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return env.WebRootPath;
    }

    private static string? FindProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);

        while (dir != null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                return dir.FullName;
            }

            if (dir.Parent == null)
            {
                break;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var single = path.Replace("//", "/");
        while (single.Contains("//"))
            single = single.Replace("//", "/");

        if (!single.StartsWith('/'))
            single = "/" + single;
        return single;
    }
}
