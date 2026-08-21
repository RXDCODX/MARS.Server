namespace MARS.Server.Services.Shikimori.Entitys;

/// <summary>
/// Аниме из Shikimori API (GraphQL).
/// </summary>
public class ShikimoriAnime
{
    public long Id { get; init; }

    public string? Name { get; init; }

    public string? Russian { get; init; }

    public string? Description { get; init; }

    public int? AiredOnYear { get; init; }

    /// <summary>
    /// Относительный путь к постеру (без домена).
    /// </summary>
    public string? ImageUrl { get; init; }
}
