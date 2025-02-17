namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiAnime
{
    public long id { get; set; }
    public required string name { get; set; }
    public required string russian { get; set; }
    public required ShikiImage image { get; set; }
    public required string url { get; set; }
    public required string kind { get; set; }
    public required string score { get; set; }
    public required string status { get; set; }
    public long episodes { get; set; }
    public long episodes_aired { get; set; }
    public required object aired_on { get; set; }
    public required object released_on { get; set; }
    public required List<object> roles { get; set; }
    public required string role { get; set; }
}
