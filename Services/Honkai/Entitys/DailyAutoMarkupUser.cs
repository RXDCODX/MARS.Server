using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Honkai.Entitys;

public class DailyAutoMarkupUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [MaxLength(50)]
    public string? TwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch (опционально)
    /// </summary>
    [ForeignKey(nameof(TwitchId))]
    public TwitchUser? TwitchUser { get; set; }

    public long? TelegramId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public required string LtmidV2 { get; set; }
    public required string LTokenV2 { get; set; }
    public required string LtuidV2 { get; set; }
    public DateTime LastAutoMarkup { get; set; }
}
