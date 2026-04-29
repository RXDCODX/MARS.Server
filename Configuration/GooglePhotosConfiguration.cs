namespace MARS.Server.Configuration;

public class GooglePhotosConfiguration
{
    public const string SectionName = "GooglePhotos";

    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public long TelegramChatId { get; set; } = -1001803337348;

    public long? TelegramAdminId { get; set; } = null;
}
