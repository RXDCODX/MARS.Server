using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using MARS.Server.Services.CinemaQueue.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MARS.Server.Services.CinemaQueue.Services;

public interface IKinopoiskService
{
    Task<KinopoiskMovieDto?> GetMovieByIdAsync(
        int movieId,
        CancellationToken cancellationToken = default
    );
    Task<KinopoiskMovieDto?> GetMovieByUrlAsync(
        string url,
        CancellationToken cancellationToken = default
    );
}

public class KinopoiskService(
    IHttpClientFactory httpClientFactory,
    IOptions<KinopoiskConfiguration> kinopoiskOptions,
    ILogger<KinopoiskService> logger
) : IKinopoiskService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly KinopoiskConfiguration _config = kinopoiskOptions.Value;
    private static readonly Regex KinopoiskUrlRegex = new(
        @"https://www\.kinopoisk\.ru/film/(\d+)",
        RegexOptions.Compiled
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<KinopoiskMovieDto?> GetMovieByIdAsync(
        int movieId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url = $"https://api.kinopoisk.dev/v1.4/movie/{movieId}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-API-KEY", _config.Api);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var movie = JsonSerializer.Deserialize<KinopoiskMovieDto>(json, JsonOptions);

            return movie;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении фильма по ID {MovieId} из Кинопоиска",
                movieId
            );
            return null;
        }
    }

    public async Task<KinopoiskMovieDto?> GetMovieByUrlAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var match = KinopoiskUrlRegex.Match(url);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var movieId))
            {
                logger.LogWarning("Не удалось извлечь ID фильма из URL Кинопоиска: {Url}", url);
                return null;
            }

            return await GetMovieByIdAsync(movieId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении фильма по URL {Url} из Кинопоиска", url);
            return null;
        }
    }
}
