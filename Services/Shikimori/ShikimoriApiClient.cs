using System.Text.Json;
using System.Text.Json.Serialization;
using MARS.Server.Services.Shikimori.Entitys;

namespace MARS.Server.Services.Shikimori;

/// <summary>
/// Собственный клиент Shikimori API.
/// Аниме и манга получаются через GraphQL (/api/graphql),
/// персонажи — через REST (/api/characters/{id}), так как GraphQL-схема
/// не содержит связи персонажа с его работами.
/// </summary>
public class ShikimoriApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<ShikimoriApiClient> logger
) : IShikimoriApiClient
{
    public const string HttpClientName = "Shikimori";

    private const string MediaFields =
        "id name russian description airedOn { year } poster { originalUrl }";

    private const string RandomAnimeQuery =
        $"{{ animes(order: random, limit: 1, score: 7) {{ {MediaFields} }} }}";

    private const string RandomMangaQuery =
        $"{{ mangas(order: random, limit: 1, score: 7) {{ {MediaFields} }} }}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger _logger = logger;

    public async Task<ShikimoriAnime?> GetRandomAnimeAsync(
        CancellationToken cancellationToken = default
    )
    {
        ShikimoriAnime? result = null;

        try
        {
            var animes = await RequestAnimesAsync(RandomAnimeQuery, cancellationToken);

            if (animes is { Length: > 0 })
            {
                result = MapAnime(animes[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении случайного аниме из Shikimori");
        }

        return result;
    }

    public async Task<ShikimoriAnime?> GetAnimeByIdAsync(
        long id,
        CancellationToken cancellationToken = default
    )
    {
        ShikimoriAnime? result = null;

        if (id > 0)
        {
            try
            {
                var query = $"{{ animes(ids: \"{id}\") {{ {MediaFields} }} }}";
                var animes = await RequestAnimesAsync(query, cancellationToken);

                if (animes is { Length: > 0 })
                {
                    result = MapAnime(animes[0]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении аниме по ID {Id} из Shikimori", id);
            }
        }

        return result;
    }

    public async Task<ShikimoriManga?> GetRandomMangaAsync(
        CancellationToken cancellationToken = default
    )
    {
        ShikimoriManga? result = null;

        try
        {
            var mangas = await RequestMangasAsync(RandomMangaQuery, cancellationToken);

            if (mangas is { Length: > 0 })
            {
                result = MapManga(mangas[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении случайной манги из Shikimori");
        }

        return result;
    }

    public async Task<ShikimoriManga?> GetMangaByIdAsync(
        long id,
        CancellationToken cancellationToken = default
    )
    {
        ShikimoriManga? result = null;

        if (id > 0)
        {
            try
            {
                var query = $"{{ mangas(ids: \"{id}\") {{ {MediaFields} }} }}";
                var mangas = await RequestMangasAsync(query, cancellationToken);

                if (mangas is { Length: > 0 })
                {
                    result = MapManga(mangas[0]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении манги по ID {Id} из Shikimori", id);
            }
        }

        return result;
    }

    public async Task<ShikimoriCharacter?> GetCharacterByIdAsync(
        long id,
        CancellationToken cancellationToken = default
    )
    {
        ShikimoriCharacter? result = null;

        if (id > 0)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await httpClient.GetAsync(
                    $"api/characters/{id}",
                    cancellationToken
                );

                if (response.IsSuccessStatusCode)
                {
                    var character = await response.Content.ReadFromJsonAsync<CharacterNode>(
                        JsonOptions,
                        cancellationToken
                    );

                    if (character != null)
                    {
                        result = MapCharacter(character);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Shikimori вернул статус {StatusCode} при получении персонажа {Id}",
                        response.StatusCode,
                        id
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении персонажа {Id} из Shikimori", id);
            }
        }

        return result;
    }

    private async Task<AnimeNode[]?> RequestAnimesAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        AnimeNode[]? result = null;

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.PostAsJsonAsync(
            "api/graphql",
            new GraphqlRequest(query),
            cancellationToken
        );

        if (response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<GraphqlEnvelope<AnimeListData>>(
                JsonOptions,
                cancellationToken
            );

            if (envelope is { Errors: null or [] })
            {
                result = envelope.Data?.Animes;
            }
            else
            {
                _logger.LogWarning(
                    "GraphQL-запрос аниме вернул ошибки: {Errors}",
                    JoinErrors(envelope?.Errors)
                );
            }
        }
        else
        {
            _logger.LogWarning(
                "Shikimori вернул статус {StatusCode} при GraphQL-запросе аниме",
                response.StatusCode
            );
        }

        return result;
    }

    private async Task<MangaNode[]?> RequestMangasAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        MangaNode[]? result = null;

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.PostAsJsonAsync(
            "api/graphql",
            new GraphqlRequest(query),
            cancellationToken
        );

        if (response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<GraphqlEnvelope<MangaListData>>(
                JsonOptions,
                cancellationToken
            );

            if (envelope is { Errors: null or [] })
            {
                result = envelope.Data?.Mangas;
            }
            else
            {
                _logger.LogWarning(
                    "GraphQL-запрос манги вернул ошибки: {Errors}",
                    JoinErrors(envelope?.Errors)
                );
            }
        }
        else
        {
            _logger.LogWarning(
                "Shikimori вернул статус {StatusCode} при GraphQL-запросе манги",
                response.StatusCode
            );
        }

        return result;
    }

    private static string? JoinErrors(GraphqlError[]? errors)
    {
        string? result = null;

        if (errors is { Length: > 0 })
        {
            result = string.Join("; ", errors.Select(e => e.Message));
        }

        return result;
    }

    private static ShikimoriAnime MapAnime(AnimeNode node)
    {
        return new ShikimoriAnime
        {
            Id = node.Id,
            Name = node.Name,
            Russian = node.Russian,
            Description = node.Description,
            AiredOnYear = node.AiredOn?.Year,
            ImageUrl = StripOrigin(node.Poster?.OriginalUrl),
        };
    }

    private static ShikimoriManga MapManga(MangaNode node)
    {
        return new ShikimoriManga
        {
            Id = node.Id,
            Name = node.Name,
            Russian = node.Russian,
            Description = node.Description,
            AiredOnYear = node.AiredOn?.Year,
            ImageUrl = StripOrigin(node.Poster?.OriginalUrl),
        };
    }

    private static ShikimoriCharacter MapCharacter(CharacterNode node)
    {
        return new ShikimoriCharacter
        {
            Id = node.Id,
            Name = node.Name,
            Russian = node.Russian,
            Description = node.Description,
            ImageUrl = StripOrigin(node.Image?.Original),
            Animes = MapTitles(node.Animes),
            Mangas = MapTitles(node.Mangas),
        };
    }

    private static IReadOnlyList<ShikimoriTitle> MapTitles(RelatedTitleNode[]? nodes)
    {
        IReadOnlyList<ShikimoriTitle> result = [];

        if (nodes is { Length: > 0 })
        {
            result = nodes.Select(n => new ShikimoriTitle(n.Name, n.Russian)).ToArray();
        }

        return result;
    }

    /// <summary>
    /// Превращает абсолютный URL в относительный путь с query,
    /// чтобы сохранить контракт «в БД хранится путь без домена».
    /// </summary>
    private static string? StripOrigin(string? url)
    {
        string? result = null;

        if (!string.IsNullOrWhiteSpace(url))
        {
            if (
                Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            )
            {
                result = uri.PathAndQuery;
            }
            else
            {
                result = url;
            }
        }

        return result;
    }

    private sealed class GraphqlRequest(string query)
    {
        public string Query { get; init; } = query;
    }

    private sealed class GraphqlEnvelope<TData>
    {
        public TData? Data { get; init; }

        public GraphqlError[]? Errors { get; init; }
    }

    private sealed class GraphqlError
    {
        public string? Message { get; init; }
    }

    private sealed class AnimeListData
    {
        public AnimeNode[]? Animes { get; init; }
    }

    private sealed class MangaListData
    {
        public MangaNode[]? Mangas { get; init; }
    }

    private sealed class AnimeNode
    {
        public long Id { get; init; }

        public string? Name { get; init; }

        public string? Russian { get; init; }

        public string? Description { get; init; }

        public AiredOnNode? AiredOn { get; init; }

        public PosterNode? Poster { get; init; }
    }

    private sealed class MangaNode
    {
        public long Id { get; init; }

        public string? Name { get; init; }

        public string? Russian { get; init; }

        public string? Description { get; init; }

        public AiredOnNode? AiredOn { get; init; }

        public PosterNode? Poster { get; init; }
    }

    private sealed class AiredOnNode
    {
        public int? Year { get; init; }
    }

    private sealed class PosterNode
    {
        public string? OriginalUrl { get; init; }
    }

    private sealed class CharacterNode
    {
        public long Id { get; init; }

        public string? Name { get; init; }

        public string? Russian { get; init; }

        public string? Description { get; init; }

        public CharacterImageNode? Image { get; init; }

        public RelatedTitleNode[]? Animes { get; init; }

        public RelatedTitleNode[]? Mangas { get; init; }
    }

    private sealed class CharacterImageNode
    {
        public string? Original { get; init; }
    }

    private sealed class RelatedTitleNode
    {
        public string? Name { get; init; }

        public string? Russian { get; init; }
    }
}
