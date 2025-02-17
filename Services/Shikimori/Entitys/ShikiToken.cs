namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiToken
{
    public required string access_token { get; set; }
    public required string token_type { get; set; }
    public int expires_in { get; set; }
    public required string refresh_token { get; set; }
    public required string scope { get; set; }
    public int created_at { get; set; }
}
