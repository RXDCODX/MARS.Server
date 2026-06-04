namespace MARS.Server.Services.PyroAlerts.Entitys;

public class MediaMetaInfo
{
    public int TwitchPointsCost { get; set; } = 0;
    public Guid? TwitchGuid { get; set; } = Guid.Empty;
    public bool Vip { get; set; } = false;
    public required string DisplayName { get; set; }
    public bool IsLooped { get; set; } = false;
    public int Duration { get; set; } = 7; //длительность отображения на странице для изображения, по умолчанию 5 секунд
    public MediaAlertPriority Priority { get; set; } = MediaAlertPriority.Normal;

    /// <summary>
    /// From 0 to 100
    /// </summary>
    public int Volume { get; set; } = 100;
}
