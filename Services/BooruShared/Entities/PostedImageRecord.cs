using System;
using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.BooruShared.Entities;

public class PostedImageRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Source { get; set; } = "";

    public int ImageId { get; set; }

    public ulong DiscordChannelId { get; set; }

    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
}
