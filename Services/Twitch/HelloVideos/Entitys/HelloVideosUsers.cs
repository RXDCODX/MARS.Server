using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Twitch.HelloVideos.Entitys;

public class HelloVideosUsers
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required string TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [Required]
    [ForeignKey(nameof(TwitchId))]
    public required TwitchUser TwitchUser { get; set; }

    public DateTimeOffset LastTimeNotif { get; set; }

    [Required]
    public Guid MediaInfoId { get; set; }
    public required MediaInfo MediaInfo { get; set; }
}
