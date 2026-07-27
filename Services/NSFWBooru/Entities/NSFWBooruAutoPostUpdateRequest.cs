using System;

namespace MARS.Server.Services.NSFWBooru.Entities;

public class NSFWBooruAutoPostUpdateRequest
{
    public Guid Id { get; set; }

    public ulong DiscordChannelId { get; set; }

    public string Tags { get; set; } = "";

    public string CronExpression { get; set; } = "";
}
