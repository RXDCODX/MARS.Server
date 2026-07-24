using System;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostUpdateRequest
{
    public Guid Id { get; set; }

    public ulong DiscordChannelId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";
}
