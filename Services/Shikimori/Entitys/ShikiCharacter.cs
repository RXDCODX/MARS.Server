using Newtonsoft.Json;

namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiCharacter
{
    public long? id { get; set; }
    public required string name { get; set; }
    public required string russian { get; set; }
    public required ShikiImage image { get; set; }
    public required string url { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public required string altname { get; set; }

    public required string japanese { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public required string description { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public required string description_html { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public required string description_source { get; set; }

    public bool favoured { get; set; }
    public int? thread_id { get; set; }
    public int? topic_id { get; set; }
    public DateTimeOffset updated_at { get; set; }
    public required List<ShikiSeyu> seyu { get; set; }
    public required List<ShikiAnime> animes { get; set; }
    public required List<ShikiMangas> mangas { get; set; }
}
