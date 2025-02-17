using System.Net;
using System.Text.Json;
using MARS.Server.Services.Shikimori.AuthCodeService;
using MARS.Server.Services.Shikimori.Entitys;

namespace MARS.Server.Services.Shikimori;

public class ShikimoriService(
    ILogger<ShikimoriService> logger,
    IOptions<ShikimoriClientOptions> configuration,
    ShikimoriAuthorizationHelpService shikimoriAuthorizationHelpService,
    IHttpClientFactory factory
) : ITelegramusService
{
    private readonly ILogger _logger = logger;
    private readonly ShikimoriClientOptions _options =
        configuration.Value ?? throw new NullReferenceException();
    private readonly HttpClient _shikiClient = factory.CreateClient("ShikimoriClient");
    private ShikiAccessToken _accessToken = shikimoriAuthorizationHelpService
        .GetCodeFromFile()
        .ConfigureAwait(false)
        .GetAwaiter()
        .GetResult();

    public async Task<ShikiAnime> GetRandomAnime(bool recursed = false)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Get,
            string.Format(
                "{0}{1}",
                _options.ShikimoriSite,
                "/api/animes?rating=r_plus&order=random&limit=1&score=7"
            )
        );

        message.Headers.Add("User-Agent", _options.ClientName);
        message.Headers.Add("Authorization", _accessToken.Access_Token);

        var response = _shikiClient.Send(message);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (recursed)
                throw new Exception("Рекурсивный вызов GetRandomAnime()");

            _accessToken = await shikimoriAuthorizationHelpService.RefreshToken(
                _shikiClient,
                _accessToken
            );
            return await GetRandomAnime(true);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ShikiAnime[]>(responseContent)?.FirstOrDefault()
                ?? throw new InvalidOperationException();
        }

        throw new InvalidOperationException();
    }

    public async Task<ShikiAnime> GetAnimeById(long id, bool recursed = false)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Get,
            string.Format("{0}{1}{2}", _options.ShikimoriSite, "/api/animes/", id)
        );

        message.Headers.Add("User-Agent", _options.ClientName);
        message.Headers.Add("Authorization", _accessToken.Access_Token);

        var response = _shikiClient.Send(message);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (recursed)
                throw new Exception("Рекурсивный вызов GetAnimeById()");

            _accessToken = await shikimoriAuthorizationHelpService.RefreshToken(
                _shikiClient,
                _accessToken
            );
            return await GetAnimeById(id, true);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ShikiAnime[]>(responseContent)?.FirstOrDefault()
                ?? throw new InvalidOperationException();
        }

        throw new InvalidOperationException();
    }

    public async Task<ShikiMangas> GetRandomManga(bool recursed = false)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Get,
            string.Format(
                "{0}{1}",
                _options.ShikimoriSite,
                "/api/mangas?censored=true&order=random&limit=1&score=7"
            )
        );

        message.Headers.Add("User-Agent", _options.ClientName);
        message.Headers.Add("Authorization", _accessToken.Access_Token);

        var response = _shikiClient.Send(message);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (recursed)
                throw new Exception("Рекурсивный вызов GetRandomManga()");

            _accessToken = await shikimoriAuthorizationHelpService.RefreshToken(
                _shikiClient,
                _accessToken
            );
            return await GetRandomManga(true);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ShikiMangas[]>(responseContent)?.FirstOrDefault()
                ?? throw new InvalidOperationException();
        }

        throw new InvalidOperationException();
    }

    public async Task<ShikiMangas> GetMangaById(long id, bool recursed = false)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Get,
            string.Format("{0}{1}{2}", _options.ShikimoriSite, "/api/mangas/", id)
        );

        message.Headers.Add("User-Agent", _options.ClientName);
        message.Headers.Add("Authorization", _accessToken.Access_Token);

        var response = _shikiClient.Send(message);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (recursed)
                throw new Exception("Рекурсивный вызов GetMangaById()");

            _accessToken = await shikimoriAuthorizationHelpService.RefreshToken(
                _shikiClient,
                _accessToken
            );
            return await GetMangaById(id, true);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ShikiMangas[]>(responseContent)?.FirstOrDefault()
                ?? throw new InvalidOperationException();
        }

        throw new InvalidOperationException();
    }

    public async Task<ShikiCharacter?> GetShikiCharacterById(string id, bool recursed = false)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Get,
            string.Format("{0}{1}{2}", _options.ShikimoriSite, "/api/characters/", id)
        );

        message.Headers.Add("User-Agent", _options.ClientName);
        message.Headers.Add("Authorization", _accessToken.Access_Token);

        var response = _shikiClient.Send(message);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (recursed)
            {
                var template = "Рекурсивный вызов GetShikiCharacterById";
                _logger.LogCritical("{0}{1}", template, "! Приложение завершает свою работу");
                throw new Exception(template);
            }

            _accessToken = await shikimoriAuthorizationHelpService.RefreshToken(
                _shikiClient,
                _accessToken
            );
            return await GetShikiCharacterById(id, true);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var waifu = JsonSerializer.Deserialize<ShikiCharacter>(responseContent);
            return waifu;
        }

        return null;
    }
}
