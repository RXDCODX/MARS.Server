namespace MARS.Server.Hubs.Models.TunaHub;

public class UserInfo
{
    [JsonProperty("uid")]
    [JsonPropertyName("uid")]
    public int? Uid;

    [JsonProperty("login")]
    [JsonPropertyName("login")]
    public required string Login;

    [JsonProperty("displayName")]
    [JsonPropertyName("displayName")]
    public required string DisplayName;

    [JsonProperty("fullName")]
    [JsonPropertyName("fullName")]
    public required string FullName;
}
