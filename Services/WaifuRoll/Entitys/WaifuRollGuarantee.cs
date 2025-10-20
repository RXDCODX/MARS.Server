using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("WaifuRollGuarantees")]
public class WaifuRollGuarantee
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

    public int RollCount { get; set; } = 0;

    public DateTimeOffset LastRoll { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
