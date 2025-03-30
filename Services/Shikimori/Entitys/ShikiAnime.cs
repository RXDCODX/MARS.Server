#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiAnime
{
    public long id { get; set; }
    public string name { get; set; }
    public string russian { get; set; }
    public ShikiImage image { get; set; }
    public string url { get; set; }
    public string kind { get; set; }
    public string score { get; set; }
    public string status { get; set; }
    public long episodes { get; set; }
    public long episodes_aired { get; set; }
    public object aired_on { get; set; }
    public object released_on { get; set; }
    public List<object> roles { get; set; }
    public string role { get; set; }
}
