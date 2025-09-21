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
        var animes = await _client.Animes.GetAnime(
            new AnimeRequestSettings
            {
                order = ShikimoriSharp.Enums.Order.random,
                limit = 1,
                score = 7,
            }
        );
        return animes?.FirstOrDefault();
    }

    public async Task<AnimeID?> GetAnimeById(long id)
    {
        var anime = await _client.Animes.GetAnime(id);
        return anime;
    }

    public async Task<Manga?> GetRandomManga()
    {
        var mangas = await _client.Mangas.GetBySearch(
            new MangaRequestSettings
            {
                order = ShikimoriSharp.Enums.Order.random,
                limit = 1,
                score = 7,
            }
        );
        return mangas?.FirstOrDefault();
    }

    public async Task<MangaID?> GetMangaById(long id)
    {
        var manga = await _client.Mangas.GetById(id);
        return manga;
    }

    public async Task<FullCharacter?> GetShikiCharacterById(long id)
    {
        var character = await _client.Characters.GetCharacterById(id);
        return character;
    }

    public async Task<string?> GetCharacterAnimeTitle(long characterId)
    {
        var result = (string?)null;

        try
        {
            var character = await GetShikiCharacterById(characterId);
            if (character?.Animes?.Any() != true)
            {
                return result;
            }

            // Выбираем аниме с самым коротким названием
            var shortestAnime = character
                .Animes.Select(a => new
                {
                    Title = a.Russian ?? a.Name,
                    Length = (a.Russian ?? a.Name).Length,
                })
                .MinBy(a => a.Length);

            if (shortestAnime != null)
            {
                result = shortestAnime.Title;
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

        return result;
    }

    public async Task<string?> GetCharacterMangaTitle(long characterId)
    {
        var result = (string?)null;

        try
        {
            var character = await GetShikiCharacterById(characterId);
            if (character?.Mangas?.Any() != true)
            {
                return result;
            }

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
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при получении манги для персонажа {CharacterId}",
                characterId
            );
        }

        return result;
    }
}
