namespace MARS.Server.Configuration;

public class DiscordConfiguration
{
    public static readonly string Configuration = "DiscordConfig";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public string Token { get; set; }
}
