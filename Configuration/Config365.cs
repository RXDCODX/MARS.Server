namespace MARS.Server.Configuration;

public class Config365
{
    public static readonly string Configuration = "Config365";

    public required string Site { get; set; }
    public required string Login { get; set; }
    public required string Password { get; set; }
    public required long TelegramChannelId { get; set; }
}
