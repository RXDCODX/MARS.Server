using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace MARS.Server.Services.Twitch.Rewards._27_RandomArt;

public class DanbooruRandomPostService(
    IOptions<BooruConfiguration> options,
    IHttpClientFactory factory
)
{
    private readonly Uri _uri = new("https://danbooru.donmai.us/");

    /// <summary>
    /// Получить случайный пост по заданным тегам.
    /// </summary>
    public async Task<DanbooruPost[]?> GetRandomPostAsync(string tags, int limit = 3)
    {
        const string arara = "RxdcodxStreamerBot/1.0";

        using var httpClient = factory.CreateClient(arara);

        httpClient.DefaultRequestHeaders.Add("User-Agent", arara);
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {GetCredentionals()}");

        httpClient.BaseAddress = _uri;

        var posts = await GetPostsByTagsAsync(httpClient, tags, limit);
        if (posts == null || posts.Length == 0)
        {
            return null;
        }

        return posts;
    }

    private static async Task<DanbooruPost[]?> GetPostsByTagsAsync(
        HttpClient httpClient,
        string tags,
        int limit
    )
    {
        var url =
            $"posts.json?tags={Uri.EscapeDataString(tags + " random:" + limit)}&limit={limit}";

        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return DanbooruPost.FromJson(content);
    }

    private string GetCredentionals()
    {
        var configuration = options.Value;

        ArgumentNullException.ThrowIfNull(configuration);

        return Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{configuration.Login}:{configuration.ApiKey}")
        );
    }
}

public partial class DanbooruPost
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("uploader_id")]
    public int? UploaderId { get; set; }

    [JsonPropertyName("score")]
    public int? Score { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("md5")]
    public string? Md5 { get; set; }

    [JsonPropertyName("last_comment_bumped_at")]
    public DateTime? LastCommentBumpedAt { get; set; }

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("image_width")]
    public int? ImageWidth { get; set; }

    [JsonPropertyName("image_height")]
    public int? ImageHeight { get; set; }

    [JsonPropertyName("tag_string")]
    public string? TagString { get; set; }

    [JsonPropertyName("fav_count")]
    public int? FavCount { get; set; }

    [JsonPropertyName("file_ext")]
    public string? FileExt { get; set; }

    [JsonPropertyName("last_noted_at")]
    public DateTime? LastNotedAt { get; set; }

    [JsonPropertyName("parent_id")]
    public int? ParentId { get; set; }

    [JsonPropertyName("has_children")]
    public bool? HasChildren { get; set; }

    [JsonPropertyName("approver_id")]
    public int? ApproverId { get; set; } // nullable!

    [JsonPropertyName("tag_count_general")]
    public int? TagCountGeneral { get; set; }

    [JsonPropertyName("tag_count_artist")]
    public int? TagCountArtist { get; set; }

    [JsonPropertyName("tag_count_character")]
    public int? TagCountCharacter { get; set; }

    [JsonPropertyName("tag_count_copyright")]
    public int? TagCountCopyright { get; set; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }

    [JsonPropertyName("up_score")]
    public int? UpScore { get; set; }

    [JsonPropertyName("down_score")]
    public int? DownScore { get; set; }

    [JsonPropertyName("is_pending")]
    public bool? IsPending { get; set; }

    [JsonPropertyName("is_flagged")]
    public bool? IsFlagged { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }

    [JsonPropertyName("tag_count")]
    public int? TagCount { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("is_banned")]
    public bool? IsBanned { get; set; }

    [JsonPropertyName("pixiv_id")]
    public int? PixivId { get; set; } // nullable!

    [JsonPropertyName("last_commented_at")]
    public DateTime? LastCommentedAt { get; set; }

    [JsonPropertyName("has_active_children")]
    public bool? HasActiveChildren { get; set; }

    [JsonPropertyName("bit_flags")]
    public int? BitFlags { get; set; }

    [JsonPropertyName("tag_count_meta")]
    public int? TagCountMeta { get; set; }

    [JsonPropertyName("has_large")]
    public bool? HasLarge { get; set; }

    [JsonPropertyName("has_visible_children")]
    public bool? HasVisibleChildren { get; set; }

    [JsonPropertyName("media_asset")]
    public MediaAsset? MediaAsset { get; set; }

    [JsonPropertyName("tag_string_general")]
    public string? TagStringGeneral { get; set; }

    [JsonPropertyName("tag_string_character")]
    public string? TagStringCharacter { get; set; }

    [JsonPropertyName("tag_string_copyright")]
    public string? TagStringCopyright { get; set; }

    [JsonPropertyName("tag_string_artist")]
    public string? TagStringArtist { get; set; }

    [JsonPropertyName("tag_string_meta")]
    public string? TagStringMeta { get; set; }

    [JsonPropertyName("file_url")]
    public string? FileUrl { get; set; }

    [JsonPropertyName("large_file_url")]
    public string? LargeFileUrl { get; set; }

    [JsonPropertyName("preview_file_url")]
    public string? PreviewFileUrl { get; set; }
}

public partial class MediaAsset
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("md5")]
    public string? Md5 { get; set; }

    [JsonPropertyName("file_ext")]
    public string? FileExt { get; set; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }

    [JsonPropertyName("image_width")]
    public int? ImageWidth { get; set; }

    [JsonPropertyName("image_height")]
    public int? ImageHeight { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("file_key")]
    public string? FileKey { get; set; }

    [JsonPropertyName("is_public")]
    public bool? IsPublic { get; set; }

    [JsonPropertyName("pixel_hash")]
    public string? PixelHash { get; set; }

    [JsonPropertyName("variants")]
    public List<MediaVariant>? Variants { get; set; }
}

public class MediaVariant
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("file_ext")]
    public string? FileExt { get; set; }
}

public partial class DanbooruPost
{
    public static DanbooruPost[]? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var token = JToken.Parse(json);

            if (token is JArray array)
            {
                var result = new List<DanbooruPost>();
                foreach (var item in array)
                {
                    if (item is JObject obj)
                    {
                        var post = FromJObject(obj);
                        if (post != null)
                        {
                            result.Add(post);
                        }
                    }
                }
                return result.ToArray();
            }
            else if (token is JObject obj)
            {
                var post = FromJObject(obj);
                return post != null ? new[] { post } : null;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Deserialization error: {ex.Message}");
            return null;
        }
    }

    private static DanbooruPost? FromJObject(JObject obj)
    {
        if (obj == null)
        {
            return null;
        }

        var post = new DanbooruPost
        {
            Id = obj.Value<int>("id"),

            CreatedAt = ParseDateTimeOffset(obj, "created_at"),
            UploaderId = obj.Value<int?>("uploader_id"),
            Score = obj.Value<int?>("score"),
            Source = obj.Value<string>("source"),
            Md5 = obj.Value<string>("md5"),
            LastCommentBumpedAt = ParseDateTimeOffset(obj, "last_comment_bumped_at"),
            Rating = obj.Value<string>("rating"),
            ImageWidth = obj.Value<int?>("image_width"),
            ImageHeight = obj.Value<int?>("image_height"),
            TagString = obj.Value<string>("tag_string"),
            FavCount = obj.Value<int?>("fav_count"),
            FileExt = obj.Value<string>("file_ext"),
            LastNotedAt = ParseDateTimeOffset(obj, "last_noted_at"),
            ParentId = obj.Value<int?>("parent_id"),
            HasChildren = obj.Value<bool?>("has_children"),
            ApproverId = obj.Value<int?>("approver_id"),
            TagCountGeneral = obj.Value<int?>("tag_count_general"),
            TagCountArtist = obj.Value<int?>("tag_count_artist"),
            TagCountCharacter = obj.Value<int?>("tag_count_character"),
            TagCountCopyright = obj.Value<int?>("tag_count_copyright"),
            FileSize = obj.Value<long?>("file_size"),
            UpScore = obj.Value<int?>("up_score"),
            DownScore = obj.Value<int?>("down_score"),
            IsPending = obj.Value<bool?>("is_pending"),
            IsFlagged = obj.Value<bool?>("is_flagged"),
            IsDeleted = obj.Value<bool?>("is_deleted"),
            TagCount = obj.Value<int?>("tag_count"),
            UpdatedAt = ParseDateTimeOffset(obj, "updated_at"),
            IsBanned = obj.Value<bool?>("is_banned"),
            PixivId = obj.Value<int?>("pixiv_id"),
            LastCommentedAt = ParseDateTimeOffset(obj, "last_commented_at"),
            HasActiveChildren = obj.Value<bool?>("has_active_children"),
            BitFlags = obj.Value<int?>("bit_flags"),
            TagCountMeta = obj.Value<int?>("tag_count_meta"),
            HasLarge = obj.Value<bool?>("has_large"),
            HasVisibleChildren = obj.Value<bool?>("has_visible_children"),

            TagStringGeneral = obj.Value<string>("tag_string_general"),
            TagStringCharacter = obj.Value<string>("tag_string_character"),
            TagStringCopyright = obj.Value<string>("tag_string_copyright"),
            TagStringArtist = obj.Value<string>("tag_string_artist"),
            TagStringMeta = obj.Value<string>("tag_string_meta"),

            FileUrl = obj.Value<string>("file_url"),
            LargeFileUrl = obj.Value<string>("large_file_url"),
            PreviewFileUrl = obj.Value<string>("preview_file_url"),
        };

        // Десериализация MediaAsset
        var assetObj = obj["media_asset"] as JObject;
        if (assetObj != null)
        {
            post.MediaAsset = MediaAsset.FromJObject(assetObj);
        }

        return post;
    }

    private static DateTime? ParseDateTimeOffset(JObject obj, string propertyName)
    {
        var token = obj[propertyName];
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (DateTime.TryParse(token.ToString(), out var dto))
        {
            return dto;
        }

        return null;
    }
}

public partial class MediaAsset
{
    public static MediaAsset? FromJObject(JObject obj)
    {
        if (obj == null)
        {
            return null;
        }

        var asset = new MediaAsset
        {
            Id = obj.Value<int?>("id"),
            CreatedAt = ParseDateTimeOffset(obj, "created_at"),
            UpdatedAt = ParseDateTimeOffset(obj, "updated_at"),
            Md5 = obj.Value<string>("md5"),
            FileExt = obj.Value<string>("file_ext"),
            FileSize = obj.Value<long?>("file_size"),
            ImageWidth = obj.Value<int?>("image_width"),
            ImageHeight = obj.Value<int?>("image_height"),
            Duration = obj.Value<double?>("duration"),
            Status = obj.Value<string>("status"),
            FileKey = obj.Value<string>("file_key"),
            IsPublic = obj.Value<bool?>("is_public"),
            PixelHash = obj.Value<string>("pixel_hash"),
        };

        // Variants
        var variantsArray = obj["variants"] as JArray;
        if (variantsArray != null)
        {
            var variants = new List<MediaVariant>();
            foreach (var v in variantsArray)
            {
                if (v is JObject vObj)
                {
                    variants.Add(
                        new MediaVariant
                        {
                            Type = vObj.Value<string>("type"),
                            Url = vObj.Value<string>("url"),
                            Width = vObj.Value<int?>("width"),
                            Height = vObj.Value<int?>("height"),
                            FileExt = vObj.Value<string>("file_ext"),
                        }
                    );
                }
            }
            asset.Variants = variants;
        }

        return asset;
    }

    private static DateTime? ParseDateTimeOffset(JObject obj, string propertyName)
    {
        var token = obj[propertyName];
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        return DateTime.TryParse(token.ToString(), out var dto) ? dto : null;
    }
}
