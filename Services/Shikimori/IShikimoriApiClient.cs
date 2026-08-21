using MARS.Server.Services.Shikimori.Entitys;

namespace MARS.Server.Services.Shikimori;

/// <summary>
/// Собственный клиент Shikimori API: GraphQL для аниме/манги,
/// REST для персонажей (у GraphQL нет связи персонажа с работами).
/// </summary>
public interface IShikimoriApiClient
{
    Task<ShikimoriAnime?> GetRandomAnimeAsync(CancellationToken cancellationToken = default);

    Task<ShikimoriAnime?> GetAnimeByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ShikimoriManga?> GetRandomMangaAsync(CancellationToken cancellationToken = default);

    Task<ShikimoriManga?> GetMangaByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ShikimoriCharacter?> GetCharacterByIdAsync(
        long id,
        CancellationToken cancellationToken = default
    );
}
