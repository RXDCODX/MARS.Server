#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiCharacter
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("russian")]
    public string Russian { get; set; }
    [JsonPropertyName("image")]
    public ShikiImage Image { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("altname")]
    public string? Altname { get; set; }

    [JsonPropertyName("japanese")]
    public string? Japanese { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("description_html")]
    public string? DescriptionHtml { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("description_source")]
    public string? DescriptionSource { get; set; }

    [JsonPropertyName("favoured")]
    public bool Favoured { get; set; }
    [JsonPropertyName("thread_id")]
    public int? ThreadId { get; set; }
    [JsonPropertyName("topic_id")]
    public int? TopicId { get; set; }
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
    [JsonPropertyName("seyu")]
    public List<ShikiSeyu> Seyu { get; set; }
    [JsonPropertyName("animes")]
    public List<ShikiAnime> Animes { get; set; }
    [JsonPropertyName("mangas")]
    public List<ShikiMangas> Mangas { get; set; }
}
