using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("Hosts")]
public class Host
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch
    /// </summary>
    [Required]
    [ForeignKey(nameof(TwitchId))]
    public required TwitchUser TwitchUser { get; set; }

    /// <summary>
    /// Имя пользователя (дублируется из TwitchUser для обратной совместимости)
    /// </summary>
    [MaxLength(100)]
    public string? Name { get; set; }

    public DateTimeOffset WhenOrdered { get; set; }
    public string? WaifuBrideId { get; set; }
    public bool IsPrivated { get; set; }
    public long OrderCount { get; set; }
    public string? WaifuRollId { get; set; }
    public DateTimeOffset? WhenPrivated { get; set; }
    public required HostAutoHello HostGreetings { get; set; }
    public required HostCoolDown HostCoolDown { get; set; }
}
