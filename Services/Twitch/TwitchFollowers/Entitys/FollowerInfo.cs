using MARS.Server.Services.Twitch.Entitys;

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
