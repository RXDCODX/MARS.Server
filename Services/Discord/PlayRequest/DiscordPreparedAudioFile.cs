namespace MARS.Server.Services.Discord.PlayRequest;

public class DiscordPreparedAudioFile
{
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public required long FileSizeBytes { get; init; }

    public required bool IsFromCache { get; init; }

    public required int BitrateKbps { get; init; }
}
