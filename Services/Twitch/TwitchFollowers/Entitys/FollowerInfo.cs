using System.Globalization;
using Newtonsoft.Json;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.TwitchFollowers.Entitys;

/// <summary>
/// Информация о фоловере канала
/// </summary>
public class FollowerInfo
{
    /// <summary>
    /// ID пользователя
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Имя пользователя
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>
    /// Логин пользователя
    /// </summary>
    public required string UserLogin { get; set; }

    public bool IsModerator { get; set; }
    public bool IsVip { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    [JsonIgnore]
    public bool IsJustFollower => !IsModerator && !IsVip;

    /// <summary>
    /// Дата подписки на канал
    /// </summary>
    public required DateTime FollowedAt { get; set; }

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
        return new FollowerInfo
        {
            UserId = follower.UserId,
            UserName = follower.UserName,
            UserLogin = follower.UserLogin,
            FollowedAt = DateTimeOffset
                .Parse(
                    follower.FollowedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind
                )
                .LocalDateTime,
            LastUpdated = DateTime.UtcNow,
        };
    }

    public static FollowerInfo FromModerator(Moderator moderator)
    {
        return new FollowerInfo()
        {
            UserId = moderator.UserId,
            FollowedAt = DateTime.UnixEpoch,
            UserLogin = moderator.UserLogin,
            UserName = moderator.UserName,
            IsModerator = true,
            IsVip = false,
            LastUpdated = DateTime.Now,
        };
    }

    public static FollowerInfo FromVip(ChannelVIPsResponseModel vip)
    {
        return new FollowerInfo()
        {
            FollowedAt = DateTime.UnixEpoch,
            UserId = vip.UserId,
            UserLogin = vip.UserLogin,
            UserName = vip.UserName,
            IsModerator = false,
            IsVip = true,
            LastUpdated = DateTime.Now,
        };
    }

    public override string ToString()
    {
        return $"{UserName} ({UserLogin}) - подписался {FollowedAt:yyyy-MM-dd HH:mm:ss}";
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
