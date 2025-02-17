namespace MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello.Entitys;

public class AutoVideoHello
{
    [Key]
    [Required]
    public required string TwitchId { get; set; }
    public DateTimeOffset LastPostDateTime { get; set; }
    public required byte[] File { get; set; }
    public required string FileExtension { get; set; }
}
