#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Services.Shikimori.Entitys;
using System.Text.Json.Serialization;

public class ShikiMangas
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("russian")]
    public string Russian { get; set; }
    [JsonPropertyName("image")]
    public ShikiImage Image { get; set; }
    [JsonPropertyName("url")]
    public string Url { get; set; }
    [JsonPropertyName("kind")]
    public string Kind { get; set; }
    [JsonPropertyName("score")]
    public string Score { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; }
    [JsonPropertyName("volumes")]
    public long Volumes { get; set; }
    [JsonPropertyName("chapters")]
    public long Chapters { get; set; }
    [JsonPropertyName("aired_on")]
    public object AiredOn { get; set; }
    [JsonPropertyName("released_on")]
    public object ReleasedOn { get; set; }
    [JsonPropertyName("english")]
    public List<object> English { get; set; }
    [JsonPropertyName("japanese")]
    public List<object> Japanese { get; set; }
    [JsonPropertyName("synonyms")]
    public List<object> Synonyms { get; set; }
    [JsonPropertyName("license_name_ru")]
    public object LicenseNameRu { get; set; }
    [JsonPropertyName("description")]
    public object Description { get; set; }
    [JsonPropertyName("description_html")]
    public string DescriptionHtml { get; set; }
    [JsonPropertyName("description_source")]
    public object DescriptionSource { get; set; }
    [JsonPropertyName("franchise")]
    public object Franchise { get; set; }
    [JsonPropertyName("favoured")]
    public bool Favoured { get; set; }
    [JsonPropertyName("anons")]
    public bool Anons { get; set; }
    [JsonPropertyName("ongoing")]
    public bool Ongoing { get; set; }
    [JsonPropertyName("thread_id")]
    public long ThreadId { get; set; }
    [JsonPropertyName("topic_id")]
    public long TopicId { get; set; }
    [JsonPropertyName("myanimelist_id")]
    public long MyanimelistId { get; set; }
    [JsonPropertyName("rates_scores_stats")]
    public List<object> RatesScoresStats { get; set; }
    [JsonPropertyName("rates_statuses_stats")]
    public List<object> RatesStatusesStats { get; set; }
    [JsonPropertyName("licensors")]
    public List<object> Licensors { get; set; }
    [JsonPropertyName("genres")]
    public List<object> Genres { get; set; }
    [JsonPropertyName("publishers")]
    public List<object> Publishers { get; set; }
    [JsonPropertyName("user_rate")]
    public object UserRate { get; set; }
}
