using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards.Entities;

/// <summary>
/// Локальная запись о награде канала. CRUD работает с этим состоянием,
/// периодическая синхронизация приводит Twitch к этому состоянию.
/// </summary>
public class ChannelRewardRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required string Title { get; set; }

    [Required]
    public int Cost { get; set; }

    public bool IsEnabled { get; set; } = true;
    public string? Prompt { get; set; }
    public string? BackgroundColor { get; set; } = "#9146FF";
    public bool IsUserInputRequired { get; set; }
    public bool IsMaxPerStreamEnabled { get; set; }
    public int? MaxPerStream { get; set; }
    public bool IsMaxPerUserPerStreamEnabled { get; set; }
    public int? MaxPerUserPerStream { get; set; }
    public bool IsGlobalCooldownEnabled { get; set; }
    public int? GlobalCooldownSeconds { get; set; }
    public bool ShouldRedemptionsSkipRequestQueue { get; set; }

    /// <summary>
    /// Мягкое удаление. Если true — при синхронизации награда будет удалена в Twitch (если существует).
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Id награды в Twitch (если уже создана). Может быть пустым до синхронизации.
    /// </summary>
    public string? TwitchRewardId { get; set; }

    /// <summary>
    /// Для PyroAlerts: ссылка на MediaInfo, которую нужно привязать.
    /// </summary>
    public Guid? MediaInfoId { get; set; }
}
