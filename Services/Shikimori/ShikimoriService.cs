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
            new ShikimoriSharp.Settings.MangaRequestSettings
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
}
