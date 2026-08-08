using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SevenTV.Types.Rest;

namespace MARS.Server.Services.SevenTv;

public sealed class SevenTvApiService(
    IHttpClientFactory httpClientFactory,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<SevenTvApiService> logger
) : ISevenTvApiService
{
    private const string BaseUrl = "https://7tv.io/v3";
    private const string ClientName = "SevenTv";

    private HttpClient? _httpClient;
    private bool _initialized;

    private async Task<HttpClient> GetHttpClientAsync()
    {
        if (_httpClient is not null)
        {
            return _httpClient;
        }

        if (_initialized)
        {
            _httpClient = httpClientFactory.CreateClient(ClientName);
            return _httpClient;
        }

        _initialized = true;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var proxyState = await dbContext
                .RootState.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Name == RootStateKeys.SevenTvProxyUrl);

            if (!string.IsNullOrWhiteSpace(proxyState?.Value))
            {
                var proxyUrl = proxyState.Value.Trim();
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(proxyUrl),
                    UseProxy = true,
                };
                _httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
                logger.LogInformation("7TV API client configured with proxy: {ProxyUrl}", proxyUrl);
                return _httpClient;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read proxy settings from RootState for 7TV API");
        }

        _httpClient = httpClientFactory.CreateClient(ClientName);
        return _httpClient;
    }

    public async Task<User?> GetUserAsync(string userId)
    {
        try
        {
            var client = await GetHttpClientAsync();
            return await client.GetFromJsonAsync<User>($"/v3/users/{userId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get 7TV user {UserId}", userId);
            return null;
        }
    }

    public async Task<EmoteSet?> GetEmoteSetAsync(string emoteSetId)
    {
        try
        {
            var client = await GetHttpClientAsync();
            return await client.GetFromJsonAsync<EmoteSet>($"/v3/emote-sets/{emoteSetId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get 7TV emote set {EmoteSetId}", emoteSetId);
            return null;
        }
    }
}
