using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Twitch.FumoFriday.Entitys;

public class FumoUser
{
    [Key]
    [Required]
    public required string TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [Required]
    [ForeignKey(nameof(TwitchId))]
    public required TwitchUser TwitchUser { get; set; }

    public DateTimeOffset LastTime { get; set; }
}
