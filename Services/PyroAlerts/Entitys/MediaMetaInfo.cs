namespace MARS.Server.Services.PyroAlerts.Entitys;

public class MediaMetaInfo
{
    public int TwitchPointsCost { get; set; } = 0;
    public bool VIP { get; set; } = false;
    public required string DisplayName { get; set; }
    public bool IsLooped { get; set; } = false;
    public int Duration { get; set; } = 15; //длительность отображения на странице для изображения, по умолчанию 5 секунд
}
