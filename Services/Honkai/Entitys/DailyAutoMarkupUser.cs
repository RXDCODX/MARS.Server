namespace MARS.Server.Services.Honkai.Entitys;

public class DailyAutoMarkupUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    public string? TwitchId { get; set; }
    public long? TelegramId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public required string LtmidV2 { get; set; }
    public required string LTokenV2 { get; set; }
    public required string LtuidV2 { get; set; }
    public DateTime LastAutoMarkup { get; set; }
}
