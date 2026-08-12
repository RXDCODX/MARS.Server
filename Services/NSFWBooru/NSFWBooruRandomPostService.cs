using System.Text.Json;
using MARS.Server.Services.NSFWBooru.Entities;

namespace MARS.Server.Services.NSFWBooru;

public class NSFWBooruRandomPostService(
    ILogger<NSFWBooruRandomPostService> logger,
    IHttpClientFactory factory
)
{
    private const string UserAgent = "MarsBot/1.0";
    private const string BaseUrl = "https://api.rule34.xxx/index.php";

    public virtual async Task<NSFWBooruPost[]?> GetRandomPostAsync(string tags, int limit = 1)
    {
        NSFWBooruPost[]? result = null;

        try
        {
            using var httpClient = factory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

            var fetchLimit = Math.Max(limit, 100);
            var url =
                $"{BaseUrl}?page=dapi&s=post&q=index&tags={Uri.EscapeDataString(tags)}&limit={fetchLimit}&json=1";

            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "NSFWBooru API вернул {StatusCode} для тегов '{Tags}'",
                    response.StatusCode,
                    tags
                );
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var posts = JsonSerializer.Deserialize<NSFWBooruPost[]>(content);
            if (posts is null || posts.Length == 0)
            {
                return null;
            }

            if (posts.Length <= limit)
            {
                result = posts;
            }
            else
            {
                var random = new Random();
                result = posts.OrderBy(_ => random.Next()).Take(limit).ToArray();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запросе к NSFWBooru API для тегов '{Tags}'", tags);
        }

        return result;
    }
}
