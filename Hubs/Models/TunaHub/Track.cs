using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MARS.Server.Hubs.Models.TunaHub;

public class Track
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public required string Id;

    [JsonProperty("realId")]
    [JsonPropertyName("realId")]
    public required string RealId;

    [JsonProperty("available")]
    [JsonPropertyName("available")]
    public bool? Available;

    [JsonProperty("canPublish")]
    [JsonPropertyName("canPublish")]
    public bool? CanPublish;

    [JsonProperty("state")]
    [JsonPropertyName("state")]
    public required string State;

    [JsonProperty("desiredVisibility")]
    [JsonPropertyName("desiredVisibility")]
    public required string DesiredVisibility;

    [JsonProperty("filename")]
    [JsonPropertyName("filename")]
    public required string Filename;

    [JsonProperty("storageDir")]
    [JsonPropertyName("storageDir")]
    public required string StorageDir;

    [JsonProperty("durationMs")]
    [JsonPropertyName("durationMs")]
    public int? DurationMs;

    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public required string Title;

    [JsonProperty("artists")]
    [JsonPropertyName("artists")]
    public required List<Artist> Artists;

    [JsonProperty("albums")]
    [JsonPropertyName("albums")]
    public required List<Album> Albums;

    [JsonProperty("userInfo")]
    [JsonPropertyName("userInfo")]
    public required UserInfo UserInfo;

    [JsonProperty("trackSource")]
    [JsonPropertyName("trackSource")]
    public required string TrackSource;

    [JsonProperty("major")]
    [JsonPropertyName("major")]
    public required Major Major;

    [JsonProperty("availableForPremiumUsers")]
    [JsonPropertyName("availableForPremiumUsers")]
    public bool? AvailableForPremiumUsers;

    [JsonProperty("availableFullWithoutPermission")]
    [JsonPropertyName("availableFullWithoutPermission")]
    public bool? AvailableFullWithoutPermission;

    [JsonProperty("availableForOptions")]
    [JsonPropertyName("availableForOptions")]
    public required List<string> AvailableForOptions;

    [JsonProperty("disclaimers")]
    [JsonPropertyName("disclaimers")]
    public required List<object> Disclaimers;

    [JsonProperty("fileSize")]
    [JsonPropertyName("fileSize")]
    public int? FileSize;

    [JsonProperty("r128")]
    [JsonPropertyName("r128")]
    public required R128 R128;

    [JsonProperty("fade")]
    [JsonPropertyName("fade")]
    public required Fade Fade;

    [JsonProperty("previewDurationMs")]
    [JsonPropertyName("previewDurationMs")]
    public int? PreviewDurationMs;

    [JsonProperty("coverUri")]
    [JsonPropertyName("coverUri")]
    public required string CoverUri;

    [JsonProperty("derivedColors")]
    [JsonPropertyName("derivedColors")]
    public required DerivedColors DerivedColors;

    [JsonProperty("ogImage")]
    [JsonPropertyName("ogImage")]
    public required string OgImage;

    [JsonProperty("lyricsAvailable")]
    [JsonPropertyName("lyricsAvailable")]
    public bool? LyricsAvailable;

    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public required string Type;

    [JsonProperty("rememberPosition")]
    [JsonPropertyName("rememberPosition")]
    public bool? RememberPosition;

    [JsonProperty("trackSharingFlag")]
    [JsonPropertyName("trackSharingFlag")]
    public required string TrackSharingFlag;

    [JsonProperty("lyricsInfo")]
    [JsonPropertyName("lyricsInfo")]
    public required LyricsInfo LyricsInfo;

    [JsonProperty("specialAudioResources")]
    [JsonPropertyName("specialAudioResources")]
    public required List<string> SpecialAudioResources;

    [JsonProperty("version")]
    [JsonPropertyName("version")]
    public required string Version;
}
