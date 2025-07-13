namespace MARS.Server.Configuration;

public class YandexMusicConfiguration
{
    public const string SectionName = "YandexMusic";
    public required string Login { get; set; }
    public required string Password { get; set; }
}
