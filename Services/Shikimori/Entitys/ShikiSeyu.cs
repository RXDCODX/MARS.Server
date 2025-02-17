namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiSeyu
{
    public int id { get; set; }
    public required string name { get; set; }
    public required string russian { get; set; }
    public required ShikiImage image { get; set; }
    public required string url { get; set; }
}
