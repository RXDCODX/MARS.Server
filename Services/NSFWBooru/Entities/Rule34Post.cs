using System.Text.Json.Serialization;

namespace MARS.Server.Services.NSFWBooru.Entities;

public class Rule34Post
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("directory")]
    public int Directory { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("sample")]
    public int Sample { get; set; }

    [JsonPropertyName("sample_url")]
    public string? SampleUrl { get; set; }

    [JsonPropertyName("preview_url")]
    public string? PreviewUrl { get; set; }

    [JsonPropertyName("file_url")]
    public string? FileUrl { get; set; }

    [JsonPropertyName("md5")]
    public string? Md5 { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("change")]
    public int Change { get; set; }

    [JsonPropertyName("owner_id")]
    public int OwnerId { get; set; }

    [JsonPropertyName("parent_id")]
    public int? ParentId { get; set; }
}
