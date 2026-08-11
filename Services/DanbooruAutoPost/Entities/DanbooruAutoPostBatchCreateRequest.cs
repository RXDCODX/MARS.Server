using System;
using System.Text.Json.Serialization;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostBatchCreateRequest
{
    public TargetPlatform TargetPlatform { get; set; }

    [JsonNumberHandling(
        JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    )]
    public ulong DiscordChannelId { get; set; }

    public long? TelegramChannelId { get; set; }

    public string Tags { get; set; } = "";

    public int Count { get; set; }

    public double IntervalHours { get; set; }

    public DateTime? StartAtUtc { get; set; }
}
