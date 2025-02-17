namespace MARS.Server.Services.Twitch.FumoFriday.Entitys;

public class FumoUser
{
    [Key]
    [Required]
    public required string TwitchId { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset LastTime { get; set; }
}
