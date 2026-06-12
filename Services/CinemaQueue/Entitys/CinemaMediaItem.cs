using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.CinemaQueue.Entitys;

[Table("CinemaQueue")]
public class CinemaMediaItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    [Required]
    [MaxLength(1000)]
    public required string MediaUrl { get; set; }

    public MediaStatus Status { get; set; } = MediaStatus.Pending;

    public int Priority { get; set; } = 0;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? ScheduledFor { get; set; }

    [MaxLength(50)]
    public string? TwitchUserId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch, добавившего медиа
    /// </summary>
    [ForeignKey(nameof(TwitchUserId))]
    public TwitchUser? TwitchUser { get; set; }

    public string? Notes { get; set; }

    public bool IsNext { get; set; } = false;

    public DateTimeOffset? LastModified { get; set; } = DateTimeOffset.Now;
}
