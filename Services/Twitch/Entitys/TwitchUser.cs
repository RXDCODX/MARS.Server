using System.Diagnostics;
using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.Honkai.Entitys;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.FumoFriday.Entitys;
using MARS.Server.Services.Twitch.HelloVideos.Entitys;
using MARS.Server.Services.Twitch.MiniGamesStats.Entitys;

namespace MARS.Server.Services.Twitch.Entitys;

/// <summary>
/// Основная информация о пользователе Twitch
/// </summary>
[Table("TwitchUsers")]
[DebuggerDisplay("{DisplayName} ({UserLogin}) - ID: {TwitchId}")]
public class TwitchUser
{
    /// <summary>
    /// ID пользователя Twitch (primary key)
    /// </summary>
    [Key]
    [Required]
    [MaxLength(50)]
    public required string TwitchId { get; init; }

    /// <summary>
    /// Логин пользователя
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string UserLogin { get; set; }

    /// <summary>
    /// Отображаемое имя пользователя
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// Ссылка на аватарку пользователя
    /// </summary>
    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    /// Цвет ника пользователя в чате
    /// </summary>
    [MaxLength(20)]
    public string? ChatColor { get; set; }

    /// <summary>
    /// Является ли пользователь модератором
    /// </summary>
    public bool IsModerator { get; set; }

    /// <summary>
    /// Является ли пользователь VIP
    /// </summary>
    public bool IsVip { get; set; }

    /// <summary>
    /// Дата подписки на канал
    /// </summary>
    public DateTime? FollowedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления информации
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Навигационные свойства для связанных сущностей
    /// </summary>
    [NotMapped]
    public TwitchLeaderboardUser? LeaderboardStats { get; set; }

    [NotMapped]
    public FumoUser? FumoUser { get; set; }

    [NotMapped]
    public ICollection<HelloVideosUsers> HelloVideos { get; set; } = new List<HelloVideosUsers>();

    [NotMapped]
    public WaifuRollGuarantee? WaifuRollGuarantee { get; set; }

    [NotMapped]
    public ICollection<DailyAutoMarkupUser> HonkaiMarkups { get; set; } =
        new List<DailyAutoMarkupUser>();

    [NotMapped]
    public ICollection<CinemaMediaItem> CinemaQueueItems { get; set; } =
        new List<CinemaMediaItem>();

    [NotMapped]
    public ICollection<BaseTrackInfo> RequestedTracks { get; set; } = new List<BaseTrackInfo>();

    [NotMapped]
    public ICollection<PlayerState> PlayerStates { get; set; } = new List<PlayerState>();

    /// <summary>
    /// Проверить, является ли пользователь просто фоловером
    /// </summary>
    [NotMapped]
    public bool IsJustFollower => !IsModerator && !IsVip;

    public override string ToString()
    {
        return $"{DisplayName} ({UserLogin}) - ID: {TwitchId}";
    }

    public override bool Equals(object? obj)
    {
        return obj is TwitchUser other && TwitchId == other.TwitchId;
    }

    public override int GetHashCode()
    {
        return TwitchId.GetHashCode();
    }
}
