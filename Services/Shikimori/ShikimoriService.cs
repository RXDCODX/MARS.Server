using ShikimoriSharp;
using ShikimoriSharp.Bases;
using ShikimoriSharp.Classes;
using ShikimoriSharp.Settings;

namespace MARS.Server.Services.Shikimori;

public class ShikimoriService : ITelegramusService
{
    private readonly ILogger _logger;
    private readonly ShikimoriClientOptions _options;
    private readonly ShikimoriClient _client;

    public ShikimoriService(
        ILogger<ShikimoriService> logger,
        IOptions<ShikimoriClientOptions> configuration
    )
    {
        _logger = logger;
        _options = configuration.Value ?? throw new NullReferenceException();
        var settings = new ClientSettings(
            _options.ClientName,
            _options.ClientId,
            _options.ClientSecret
        );
        _client = new ShikimoriClient(logger, settings);
    }

    public async Task<Anime?> GetRandomAnime()
    {
        Anime? result = null;

        try
        {
            var animes = await _client.Animes.GetAnime(
                new AnimeRequestSettings
                {
                    order = ShikimoriSharp.Enums.Order.random,
                    limit = 1,
                    score = 7,
                }
            );
            result = animes?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении случайного аниме");
        }

        return result;
    }

    public async Task<AnimeID?> GetAnimeById(long id)
    {
        AnimeID? result = null;

        if (id > 0)
        {
            try
            {
                result = await _client.Animes.GetAnime(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении аниме по ID: {Id}", id);
            }
        }

        return result;
    }

    public async Task<Manga?> GetRandomManga()
    {
        Manga? result = null;

        try
        {
            var mangas = await _client.Mangas.GetBySearch(
                new MangaRequestSettings
                {
                    order = ShikimoriSharp.Enums.Order.random,
                    limit = 1,
                    score = 7,
                }
            );
            result = mangas?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении случайной манги");
        }

        return result;
    }

    public async Task<MangaID?> GetMangaById(long id)
    {
        MangaID? result = null;

        if (id > 0)
        {
            try
            {
                result = await _client.Mangas.GetById(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении манги по ID: {Id}", id);
            }
        }

        return result;
    }

    public async Task<FullCharacter?> GetShikiCharacterById(long id)
    {
        FullCharacter? result = null;

        if (id > 0)
        {
            try
            {
                result = await _client.Characters.GetCharacterById(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении персонажа по ID: {Id}", id);
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
                if (character?.Animes?.Length > 0)
                {
                    // Выбираем аниме с самым коротким названием
                    var shortestAnime = character
                        .Animes.Select(a => new
                        {
                            Title = a.Russian ?? a.Name,
                            (a.Russian ?? a.Name).Length,
                        })
                        .MinBy(a => a.Length);

                    if (shortestAnime != null)
                    {
                        result = shortestAnime.Title;
                    }
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
                if (character?.Mangas?.Length > 0)
                {
                    // Выбираем мангу с самым коротким названием
                    var shortestManga = character
                        .Mangas.Select(m => new
                        {
                            Title = m.Russian ?? m.Name,
                            (m.Russian ?? m.Name).Length,
                        })
                        .MinBy(m => m.Length);

                    if (shortestManga != null)
                    {
                        result = shortestManga.Title;
                    }
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
}
