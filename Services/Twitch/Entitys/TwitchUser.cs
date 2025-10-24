using System.Diagnostics;
using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.Honkai.Entitys;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.FumoFriday.Entitys;
using MARS.Server.Services.Twitch.HelloVideos.Entitys;
using MARS.Server.Services.Twitch.MiniGamesStats.Entitys;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.Models.Chat;
using ChatMessage = TwitchLib.Client.Models.ChatMessage;

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
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column(nameof(TwitchId))]
    public required string TwitchId
    {
        get { return _twitchId; }
        init
        {
            if (!IsValidTwitchId(value))
            {
                throw new ArgumentException("TwitchId was not valid");
            }
            _twitchId = value;
        }
    }

    [NotMapped]
    private readonly string _twitchId = string.Empty;

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
    /// Проверить, является ли пользователь просто фоловером
    /// </summary>
    [NotMapped]
    public bool IsSimpleUser => !IsModerator && !IsVip;

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

    /// <summary>
    /// Проверяет, является ли TwitchId валидным (должен быть числовым)
    /// </summary>
    private static bool IsValidTwitchId(string twitchId)
    {
        // TwitchId должен быть числовым (не GUID или другая строка)
        return !string.IsNullOrWhiteSpace(twitchId) && long.TryParse(twitchId, out _);
    }
}
