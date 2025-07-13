using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MARS.Server.Hubs.Models.TunaHub;

public class Album
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public int? Id;

    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public required string Title;

    [JsonProperty("metaType")]
    [JsonPropertyName("metaType")]
    public required string MetaType;

    [JsonProperty("year")]
    [JsonPropertyName("year")]
    public int? Year;

    [JsonProperty("releaseDate")]
    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate;

    [JsonProperty("coverUri")]
    [JsonPropertyName("coverUri")]
    public required string CoverUri;

    [JsonProperty("ogImage")]
    [JsonPropertyName("ogImage")]
    public required string OgImage;

    [JsonProperty("trackCount")]
    [JsonPropertyName("trackCount")]
    public int? TrackCount;

    [JsonProperty("likesCount")]
    [JsonPropertyName("likesCount")]
    public int? LikesCount;

    [JsonProperty("recent")]
    [JsonPropertyName("recent")]
    public bool? Recent;

    [JsonProperty("veryImportant")]
    [JsonPropertyName("veryImportant")]
    public bool? VeryImportant;

    [JsonProperty("artists")]
    [JsonPropertyName("artists")]
    public required List<Artist> Artists;

    [JsonProperty("labels")]
    [JsonPropertyName("labels")]
    public required List<Label> Labels;

    [JsonProperty("available")]
    [JsonPropertyName("available")]
    public bool? Available;

    [JsonProperty("availableForPremiumUsers")]
    [JsonPropertyName("availableForPremiumUsers")]
    public bool? AvailableForPremiumUsers;

    [JsonProperty("availableForOptions")]
    [JsonPropertyName("availableForOptions")]
    public required List<string> AvailableForOptions;

    [JsonProperty("availableForMobile")]
    [JsonPropertyName("availableForMobile")]
    public bool? AvailableForMobile;

    [JsonProperty("availablePartially")]
    [JsonPropertyName("availablePartially")]
    public bool? AvailablePartially;

    [JsonProperty("bests")]
    [JsonPropertyName("bests")]
    public required List<int?> Bests;

    [JsonProperty("disclaimers")]
    [JsonPropertyName("disclaimers")]
    public required List<string> Disclaimers;

    [JsonProperty("listeningFinished")]
    [JsonPropertyName("listeningFinished")]
    public bool? ListeningFinished;

    [JsonProperty("trackPosition")]
    [JsonPropertyName("trackPosition")]
    public required TrackPosition TrackPosition;

    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public required string Type;

    [JsonProperty("genre")]
    [JsonPropertyName("genre")]
    public required string Genre;

    [JsonProperty("version")]
    [JsonPropertyName("version")]
    public required string Version;

    [JsonProperty("contentWarning")]
    [JsonPropertyName("contentWarning")]
    public required string ContentWarning;
}
