using Newtonsoft.Json;

namespace MARS.Server.Services.TelegramBotService.Entitys;

public class TelegramUser
{
    public required string Name { get; set; }

    [JsonProperty("User")]
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long UserId { get; set; }

    [JsonProperty("LastTime")]
    public DateTimeOffset LastTimeMessage { get; set; }

    public bool RaidHelper { get; set; } = false;
    public bool PyroAlertsAccess { get; set; } = false;
    public bool HonkaiNotifications { get; set; } = false;
    public bool StreamUpNotifications { get; set; } = false;
    public bool ZenlessZoneZeroDailyNotif { get; set; } = false;
    public bool GenshinImpactDailyNotif { get; set; } = false;
    public DateTimeOffset ByeByeLastMessageTime { get; set; } = DateTimeOffset.Now;
    public bool ByeByeServiceNotification { get; set; } = false;
}
