namespace MARS.Server.Configuration;

public class YouTubeConfig
{
    public const string SectionName = "YouTubeConfig";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public string Token { get; set; }
}
