namespace MARS.Server.Services.Shikimori.Entitys;
using System.Text.Json.Serialization;

public class ShikiToken
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }
    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; set; }
    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; set; }
    [JsonPropertyName("scope")]
    public required string Scope { get; set; }
    [JsonPropertyName("created_at")]
    public required int CreatedAt { get; set; }
}
