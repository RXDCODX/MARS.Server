using System.Globalization;
using MARS.Server.Services.Twitch.Entitys;
using Newtonsoft.Json;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;

namespace MARS.Server.Services.Twitch.TwitchFollowers.Entitys;

/// <summary>
/// Информация о фоловере канала
/// </summary>
public class FollowerInfo
{
    /// <summary>
    /// ID пользователя Twitch
    /// </summary>
    [Key]
    [Required]
    public required string UserId { get; init; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [Required]
    [ForeignKey(nameof(UserId))]
    public required TwitchUser TwitchUser { get; set; }

    /// <summary>
    /// Имя пользователя (дублируется из TwitchUser для обратной совместимости)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Логин пользователя (дублируется из TwitchUser для обратной совместимости)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string UserLogin { get; set; } = string.Empty;

    /// <summary>
    /// Отображаемое имя пользователя (дублируется из TwitchUser для обратной совместимости)
    /// </summary>
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Ссылка на аватарку пользователя (дублируется из TwitchUser для обратной совместимости)
    /// </summary>
    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    /// Цвет ника пользователя в чате (дублируется из TwitchUser для обратной совместимости)
    /// </summary>
    [MaxLength(20)]
    public string? ChatColor { get; set; }

    /// <summary>
    /// Является ли пользователь модератором (дублируется из TwitchUser для обратной совместимости)
    /// </summary>
    public bool IsModerator { get; set; }

    /// <summary>
    /// Является ли пользователь VIP (дублируется из TwitchUser для обратной совместимости)
    /// </summary>
    public bool IsVip { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    [JsonIgnore]
    public bool IsJustFollower => !IsModerator && !IsVip;

    /// <summary>
    /// Дата подписки на канал
    /// </summary>
    public DateTime FollowedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления информации
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Создать FollowerInfo из ChannelFollower
    /// </summary>
    /// <param name="follower">Объект ChannelFollower из Twitch API</param>
    /// <returns>Новый экземпляр FollowerInfo</returns>
    public static FollowerInfo FromChannelFollower(ChannelFollower follower)
    {
        var followedAt = DateTimeOffset
            .Parse(
                follower.FollowedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind
            )
            .LocalDateTime;

        var twitchUser = new TwitchUser
        {
            TwitchId = follower.UserId,
            UserLogin = follower.UserLogin,
            DisplayName = follower.UserName,
            FollowedAt = followedAt,
            LastUpdated = DateTime.UtcNow,
        };

        return new FollowerInfo
        {
            UserId = follower.UserId,
            TwitchUser = twitchUser,
            UserName = follower.UserName,
            UserLogin = follower.UserLogin,
            DisplayName = follower.UserName,
            FollowedAt = followedAt,
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Создать FollowerInfo из данных пользователя Twitch API
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="userLogin">Логин пользователя</param>
    /// <param name="displayName">Отображаемое имя</param>
    /// <param name="profileImageUrl">Ссылка на аватарку</param>
    /// <param name="chatColor">Цвет ника в чате</param>
    /// <param name="isModerator">Является ли модератором</param>
    /// <param name="isVip">Является ли VIP</param>
    /// <param name="followedAt">Дата подписки</param>
    /// <returns>Новый экземпляр FollowerInfo</returns>
    public static FollowerInfo FromUserData(
        string userId,
        string userLogin,
        string displayName,
        string? profileImageUrl = null,
        string? chatColor = null,
        bool isModerator = false,
        bool isVip = false,
        DateTime? followedAt = null
    )
    {
        var twitchUser = new TwitchUser
        {
            TwitchId = userId,
            UserLogin = userLogin,
            DisplayName = displayName,
            ProfileImageUrl = profileImageUrl,
            ChatColor = chatColor,
            IsModerator = isModerator,
            IsVip = isVip,
            FollowedAt = followedAt,
            LastUpdated = DateTime.UtcNow,
        };

        return new FollowerInfo
        {
            UserId = userId,
            TwitchUser = twitchUser,
            FollowedAt = followedAt ?? DateTime.UnixEpoch,
            LastUpdated = DateTime.UtcNow,
        };
    }

    public static FollowerInfo FromModerator(Moderator moderator)
    {
        var twitchUser = new TwitchUser
        {
            TwitchId = moderator.UserId,
            UserLogin = moderator.UserLogin,
            DisplayName = moderator.UserName,
            IsModerator = true,
            IsVip = false,
            LastUpdated = DateTime.UtcNow,
        };

        return new FollowerInfo()
        {
            UserId = moderator.UserId,
            TwitchUser = twitchUser,
            FollowedAt = DateTime.UnixEpoch,
            LastUpdated = DateTime.UtcNow,
        };
    }

    public static FollowerInfo FromVip(ChannelVIPsResponseModel vip)
    {
        var twitchUser = new TwitchUser
        {
            TwitchId = vip.UserId,
            UserLogin = vip.UserLogin,
            DisplayName = vip.UserName,
            IsModerator = false,
            IsVip = true,
            LastUpdated = DateTime.UtcNow,
        };

        return new FollowerInfo()
        {
            UserId = vip.UserId,
            TwitchUser = twitchUser,
            FollowedAt = DateTime.UnixEpoch,
            LastUpdated = DateTime.UtcNow,
        };
    }

    public override string ToString()
    {
        return $"{TwitchUser.DisplayName} ({TwitchUser.UserLogin}) - подписался {FollowedAt:yyyy-MM-dd HH:mm:ss}";
    }

    public override bool Equals(object? obj)
    {
        return obj is FollowerInfo other && UserId == other.UserId;
    }

    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }
}
