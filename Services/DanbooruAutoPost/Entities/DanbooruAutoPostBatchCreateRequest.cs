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

    public string CronExpression { get; set; } = "";

    public DateTime EndAtUtc { get; set; }
}
