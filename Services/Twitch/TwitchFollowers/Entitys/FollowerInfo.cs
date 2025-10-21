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
    [ForeignKey(nameof(UserId))]
    public TwitchUser? TwitchUser { get; set; }
}
