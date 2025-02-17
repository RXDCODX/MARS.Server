namespace MARS.Server.Services.Shikimori.Entitys;

public class SearchCharacter
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Russian { get; set; }
    public required ShikiImage Image { get; set; }
    public required string Url { get; set; }
}
