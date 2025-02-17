namespace MARS.Server.Configuration;

public class TwitchConfiguration
{
    public static string SectionName { get; set; } = "TwitchConfig";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}
