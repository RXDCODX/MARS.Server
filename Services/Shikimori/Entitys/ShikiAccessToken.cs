using System.Text.Json.Serialization;

namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiAccessToken
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public required string Scope { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
}
