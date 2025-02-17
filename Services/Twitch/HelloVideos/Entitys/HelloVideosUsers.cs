namespace MARS.Server.Services.Twitch.HelloVideos.Entitys;

public class HelloVideosUsers
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset LastTimeNotif { get; set; }

    [Required]
    public required string TwitchId { get; set; }

    [Required]
    public Guid MediaInfoId { get; set; }
    public required MediaInfo MediaInfo { get; set; }
}
