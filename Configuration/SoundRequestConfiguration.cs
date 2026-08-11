namespace MARS.Server.Configuration;

public class SoundRequestConfiguration
{
    public const string SectionName = "SoundRequest";

    public SoundRequestProvider Provider { get; set; } = SoundRequestProvider.YouTube;

    public string[] EnabledPlatforms { get; set; } = [];
}

public enum SoundRequestProvider
{
    Null = -1,
    YouTube = 0,
    Spotify = 1,
}
