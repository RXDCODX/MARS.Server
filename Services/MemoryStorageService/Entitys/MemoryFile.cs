namespace MARS.Server.Services.MemoryStorageService.Entitys;

/// <summary>
/// Represents a file stored in memory, used by the memory storage service.
/// </summary>
public class MemoryFile
{
    public required string FileName { get; init; }
    public ushort UseCount { get; set; }
    public required MediaType MediaType { get; init; }
    public required byte[] FileContent
    {
        get =>
            field is not { Length: > 0 }
                ? throw new NullReferenceException("Trying get empty content")
                : field;
        set
        {
            if (value is not { Length: > 0 })
            {
                throw new NullReferenceException("Empty content is not allowed");
            }

            field = value;
        }
    }
    public required string Exstension
    {
        get => string.IsNullOrWhiteSpace(field) ? string.Empty : field;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new NullReferenceException("Empty string is not allowed");
            }
            else if (!value.Contains('.'))
            {
                throw new InvalidDataException("Only point extension is not allowed");
            }

            field = value;
        }
    }

    public string GetContentType()
    {
        switch (MediaType)
        {
            case MediaType.Audio:
                return "audio/" + Exstension;
            case MediaType.Gif:
                return "image/gif";
            case MediaType.Image:
                return "image/" + Exstension;
            case MediaType.TelegramSticker:
                return "video/lottie+json";
            case MediaType.Video:
                return "video/" + Exstension;
            case MediaType.Voice:
                goto case MediaType.Audio;
            case MediaType.None:
                return "none";
            default:
                return "none";
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is MemoryFile newDescription
            && FileName.Equals(newDescription.FileName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FileName, MediaType);
    }
}
