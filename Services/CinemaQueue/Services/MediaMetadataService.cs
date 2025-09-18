using System.Text.RegularExpressions;
using MARS.Server.Services.Shikimori;

namespace MARS.Server.Services.CinemaQueue.Services;

public interface IMediaMetadataService
{
    Task<MediaMetadata?> GetMetadataAsync(
        string url,
        CancellationToken cancellationToken = default
    );
}

public class MediaMetadata
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? SourceUrl { get; set; }
}

public class MediaMetadataService(
    IKinopoiskService kinopoiskService,
    ShikimoriService shikimoriService,
    ILogger<MediaMetadataService> logger
) : IMediaMetadataService
{
    private static readonly Regex KinopoiskUrlRegex = new(
        @"https://www\.kinopoisk\.ru/film/(\d+)",
        RegexOptions.Compiled
    );
    private static readonly Regex ShikimoriUrlRegex = new(
        @"https://shikimori\.one/animes/(\d+)-",
        RegexOptions.Compiled
    );

    public async Task<MediaMetadata?> GetMetadataAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            // Проверяем, является ли ссылка Кинопоиском
            if (KinopoiskUrlRegex.IsMatch(url))
            {
                return await GetKinopoiskMetadataAsync(url, cancellationToken);
            }

            // Проверяем, является ли ссылка Шикимори
            if (ShikimoriUrlRegex.IsMatch(url))
            {
                return await GetShikimoriMetadataAsync(url);
            }

            logger.LogWarning("Неподдерживаемый домен для URL: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении метаданных для URL: {Url}", url);
            return null;
        }
    }

    private async Task<MediaMetadata?> GetKinopoiskMetadataAsync(
        string url,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var movie = await kinopoiskService.GetMovieByUrlAsync(url, cancellationToken);
            if (movie == null)
            {
                logger.LogWarning("Фильм не найден в Кинопоиске для URL: {Url}", url);
                return null;
            }

            // Формируем title с годом, если есть
            var title = movie.Name;
            if (!string.IsNullOrWhiteSpace(title) && movie.Year.HasValue)
            {
                title = $"{title} ({movie.Year})";
            }

            // Используем description или shortDescription
            var description = movie.Description ?? movie.ShortDescription;

            // Используем постер, если есть
            var imageUrl = movie.Poster?.Url;

            if (string.IsNullOrWhiteSpace(title))
            {
                logger.LogWarning("Не удалось получить title для фильма из Кинопоиска: {Url}", url);
                return null;
            }

            return new MediaMetadata
            {
                Title = title,
                Description = description,
                ImageUrl = imageUrl,
                SourceUrl = url,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении метаданных Кинопоиска для URL: {Url}", url);
            return null;
        }
    }

    private async Task<MediaMetadata?> GetShikimoriMetadataAsync(string url)
    {
        try
        {
            var match = ShikimoriUrlRegex.Match(url);
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out var animeId))
            {
                logger.LogWarning("Не удалось извлечь ID аниме из URL Шикимори: {Url}", url);
                return null;
            }

            var anime = await shikimoriService.GetAnimeById(animeId);
            if (anime == null)
            {
                logger.LogWarning("Аниме не найдено в Шикимори для ID: {AnimeId}", animeId);
                return null;
            }

            return new MediaMetadata
            {
                Title = $"{anime.Russian ?? anime.Name} ({anime.AiredOn?.Year})",
                Description = anime.Description,
                ImageUrl = anime.Image?.Original,
                SourceUrl = url,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении метаданных Шикимори для URL: {Url}", url);
            return null;
        }
    }
}
