#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiSeyu
{
    public int id { get; set; }
    public string? name { get; set; }
    public string? russian { get; set; }
    public ShikiImage image { get; set; }
    public string? url { get; set; }
}
