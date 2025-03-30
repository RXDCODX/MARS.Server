#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
using Newtonsoft.Json;

namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiCharacter
{
    public long? id { get; set; }
    public string? name { get; set; }
    public required string russian { get; set; }
    public ShikiImage image { get; set; }
    public string? url { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? altname { get; set; }

    public string? japanese { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? description { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? description_html { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? description_source { get; set; }

    public bool favoured { get; set; }
    public int? thread_id { get; set; }
    public int? topic_id { get; set; }
    public DateTimeOffset updated_at { get; set; }
    public List<ShikiSeyu> seyu { get; set; }
    public List<ShikiAnime> animes { get; set; }
    public List<ShikiMangas> mangas { get; set; }
}
