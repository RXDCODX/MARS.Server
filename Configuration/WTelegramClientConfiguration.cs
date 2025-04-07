namespace MARS.Server.Configuration;

public class WTelegramClientConfiguration
{
    public static readonly string TelegramSection = "WTelegram";
    public int AppId { get; set; }
    public required string ApiHash { get; set; }
    public required string FirstNameLastName { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Password { get; set; }
}
