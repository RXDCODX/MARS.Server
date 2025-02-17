using Newtonsoft.Json;

namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiAccessToken
{
    [JsonProperty("access_token")]
    public required string Access_Token { get; set; }

    [JsonProperty("token_type")]
    public required string TokenType { get; set; } = "Bearer";

    [JsonProperty("expires_in")]
    public required int ExpiresIn { get; set; }

    [JsonProperty("refresh_token")]
    public required string RefreshToken { get; set; }

    [JsonProperty("scope")]
    public required string Scope { get; set; }

    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }
}
