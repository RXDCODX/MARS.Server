namespace MARS.Server.Configuration;

public class SpotifySoundRequestConfiguration
{
    public const string SectionName = "SpotifySoundRequest";

    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public bool ForceDeviceTransfer { get; set; } = true;

    public string Market { get; set; } = "RU";

    public int PollingIntervalMs { get; set; } = 2000;
}
