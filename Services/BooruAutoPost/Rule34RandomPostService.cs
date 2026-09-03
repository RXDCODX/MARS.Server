using System.Text.Json;
using MARS.Server.Services.BooruAutoPost.Entities;

namespace MARS.Server.Services.BooruAutoPost;

public class Rule34RandomPostService(
    ILogger<Rule34RandomPostService> logger,
    IHttpClientFactory factory
)
{
    private const string UserAgent = "MarsBot/1.0";
    private const string BaseUrl = "https://api.rule34.xxx/index.php";

    public virtual async Task<Rule34Post[]?> GetRandomPostAsync(string tags, int limit = 1)
    {
        Rule34Post[]? result = null;

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
                    "Rule34 API вернул {StatusCode} для тегов '{Tags}'",
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

            var posts = JsonSerializer.Deserialize<Rule34Post[]>(content);
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
            logger.LogError(ex, "Ошибка при запросе к Rule34 API для тегов '{Tags}'", tags);
        }

        return result;
    }
}
