#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiImage
{
    public required string original { get; set; }
    public string? preview { get; set; }
    public string? x96 { get; set; }
    public string? x48 { get; set; }
}
