using Newtonsoft.Json;

namespace MARS.Server.Services.AutoArts.Entitys;

public class Image
{
    [JsonProperty("signature")]
    public string? Signature { get; set; }

    [JsonProperty("exstension")]
    public string? Extension { get; set; }

    [JsonProperty("image_id")]
    public int ImageID { get; set; }

    [JsonProperty("favorites")]
    public int Favorites { get; set; }

    [JsonProperty("dominant_color")]
    public string? DominantColor { get; set; }

    [JsonProperty("source")]
    public string? Source { get; set; }

    [JsonProperty("artist")]
    public object? Artist { get; set; }

    [JsonProperty("uploaded_at")]
    public DateTimeOffset UploadedAt { get; set; }

    [JsonProperty("liked_at")]
    public object? LikedAt { get; set; }

    [JsonProperty("is_nsfw")]
    public bool IsNsfw { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("byte_size")]
    public int ByteSize { get; set; }

    [JsonProperty("url")]
    public string? URL { get; set; }

    [JsonProperty("preview_url")]
    public string? PreviewURL { get; set; }
}
