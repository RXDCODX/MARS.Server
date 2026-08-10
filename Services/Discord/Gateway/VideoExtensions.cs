namespace MARS.Server.Services.Discord.Gateway;

public static class VideoExtensions
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp",
        ".bmp",
        ".tiff",
        ".tif",
    };

    private static readonly HashSet<string> VideoFileExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".mp4",
        ".mkv",
        ".webm",
        ".mov",
        ".avi",
        ".wmv",
        ".mpeg",
        ".mpg",
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".ogg",
        ".aac",
        ".flac",
        ".wav",
        ".opus",
        ".weba",
    };

    public static bool IsImageFile(string fileName)
    {
        return ImageExtensions.Contains(Path.GetExtension(fileName));
    }

    public static bool IsVideoFile(string fileName)
    {
        return VideoFileExtensions.Contains(Path.GetExtension(fileName));
    }

    public static bool IsAudioFile(string fileName)
    {
        return AudioExtensions.Contains(Path.GetExtension(fileName));
    }
}
