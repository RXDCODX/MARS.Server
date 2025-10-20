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
    [ForeignKey(nameof(TwitchId))]
    public TwitchUser? TwitchUser { get; set; }

    /// <summary>
    /// Имя пользователя (дублируется для обратной совместимости)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset LastTimeNotif { get; set; }

    [Required]
    public Guid MediaInfoId { get; set; }
    public required MediaInfo MediaInfo { get; set; }
}
