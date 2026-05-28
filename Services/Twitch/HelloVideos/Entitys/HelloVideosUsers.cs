namespace MARS.Server.Services.Twitch.HelloVideos.Entitys;

public class HelloVideosUsers
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [ForeignKey(nameof(TwitchId))]
    public TwitchUser? TwitchUser { get; set; }

    public DateTimeOffset LastTimeNotif { get; set; }

    [Required]
    public Guid MediaInfoId { get; set; }

    [ForeignKey(nameof(MediaInfoId))]
    public required MediaInfo MediaInfo { get; set; }
}
