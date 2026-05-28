namespace MARS.Server.Services.Twitch.FumoFriday.Entitys;

public class FumoUser
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [ForeignKey(nameof(TwitchId))]
    public TwitchUser? TwitchUser { get; set; }

    public DateTimeOffset LastTime { get; set; }
}
