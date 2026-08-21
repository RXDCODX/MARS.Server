using MARS.Server.Exstensions;
using MARS.Server.Services.Shikimori.Entitys;
using MARS.Server.Services.Telegram;

namespace MARS.Server.Services.Shikimori;

public class ShikimoriService(
    ILogger<ShikimoriService> logger,
    IShikimoriApiClient apiClient,
    IShikimoriRateLimiter shikimoriRateLimiter
) : ITelegramusService
{
    private readonly ILogger _logger = logger;
    private readonly IShikimoriApiClient _apiClient = apiClient;
    private readonly IShikimoriRateLimiter _shikimoriRateLimiter = shikimoriRateLimiter;

    public async Task<ShikimoriAnime?> GetRandomAnime()
    {
        ShikimoriAnime? result = null;

        try
        {
            await _shikimoriRateLimiter.WaitForSlotAsync();
            result = await _apiClient.GetRandomAnimeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении случайного аниме");
        }

        return result;
    }

    public async Task<ShikimoriAnime?> GetAnimeById(long id)
    {
        ShikimoriAnime? result = null;

        if (id > 0)
        {
            try
            {
                await _shikimoriRateLimiter.WaitForSlotAsync();
                result = await _apiClient.GetAnimeByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении аниме по ID: {Id}", id);
            }
        }

        return result;
    }

    public async Task<ShikimoriManga?> GetRandomManga()
    {
        ShikimoriManga? result = null;

        try
        {
            await _shikimoriRateLimiter.WaitForSlotAsync();
            result = await _apiClient.GetRandomMangaAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении случайной манги");
        }

        return result;
    }

    public async Task<ShikimoriManga?> GetMangaById(long id)
    {
        ShikimoriManga? result = null;

        if (id > 0)
        {
            try
            {
                await _shikimoriRateLimiter.WaitForSlotAsync();
                result = await _apiClient.GetMangaByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении манги по ID: {Id}", id);
            }
        }

        return result;
    }

    public async Task<ShikimoriCharacter?> GetShikiCharacterById(long id)
    {
        ShikimoriCharacter? result = null;

        if (id > 0)
        {
            try
            {
                await _shikimoriRateLimiter.WaitForSlotAsync();
                result = await _apiClient.GetCharacterByIdAsync(id);
            }
            catch (Exception ex)
            {
                var exception = new Exception($"Ошибка при получении персонажа по ID: {id}", ex);
                _logger.LogException(exception);
            }
        }

        return result;
    }

    public async Task<string?> GetCharacterAnimeTitle(long characterId)
    {
        string? result = null;

        if (characterId > 0)
        {
            try
            {
                var character = await GetShikiCharacterById(characterId);

                if (character?.Animes is { Count: > 0 })
                {
                    // Выбираем аниме с самым коротким названием
                    result = character
                        .Animes.Select(a => a.Russian ?? a.Name)
                        .Where(title => !string.IsNullOrWhiteSpace(title))
                        .MinBy(title => title!.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ошибка при получении аниме для персонажа {CharacterId}",
                    characterId
                );
            }
        }

        return result;
    }

    public async Task<string?> GetCharacterMangaTitle(long characterId)
    {
        string? result = null;

        if (characterId > 0)
        {
            try
            {
                var character = await GetShikiCharacterById(characterId);

                if (character?.Mangas is { Count: > 0 })
                {
                    // Выбираем мангу с самым коротким названием
                    result = character
                        .Mangas.Select(m => m.Russian ?? m.Name)
                        .Where(title => !string.IsNullOrWhiteSpace(title))
                        .MinBy(title => title!.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ошибка при получении манги для персонажа {CharacterId}",
                    characterId
                );
            }
        }

        return result;
    }

    /// <summary>
    /// Получает информацию о состоянии рейт лимитера
    /// </summary>
    /// <returns>Информация о доступных слотах</returns>
    public RateLimiterInfo GetRateLimiterInfo()
    {
        return _shikimoriRateLimiter.GetInfo();
    }
}
