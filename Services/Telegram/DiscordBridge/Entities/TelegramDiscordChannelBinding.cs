using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.Telegram.DiscordBridge.Entities;

/// <summary>
/// Связь Telegram канала и Discord канала (many-to-many)
/// </summary>
public class TelegramDiscordChannelBinding
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public long TelegramChannelId { get; set; }

    public ulong DiscordChannelId { get; set; }

    public bool IsEnabled { get; set; } = true;

    [MaxLength(500)]
    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.Now;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.Now;
}
