namespace MARS.Server.Services.Discord.PlayRequest;

public class DiscordPlaySelectionSession
{
    public required string SessionId { get; init; }

    public required ulong ChannelId { get; init; }

    public required ulong UserId { get; init; }

    public ulong MessageId { get; set; }

    public required string Query { get; init; }

    public required IReadOnlyList<BaseTrackInfo> Tracks { get; init; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public bool IsExpired(TimeSpan lifetime)
    {
        var result = DateTime.UtcNow - CreatedAtUtc > lifetime;

        return result;
    }
}
