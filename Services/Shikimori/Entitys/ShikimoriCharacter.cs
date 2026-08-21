namespace MARS.Server.Services.Shikimori.Entitys;

/// <summary>
/// Персонаж Shikimori со списками связанных аниме и манг.
/// Данные берутся из REST API (/api/characters/{id}), так как GraphQL-схема
/// не содержит связи персонажа с его работами.
/// </summary>
public class ShikimoriCharacter
{
    public long Id { get; init; }

    public string? Name { get; init; }

    public string? Russian { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// Относительный путь к изображению (без домена).
    /// </summary>
    public string? ImageUrl { get; init; }

    public IReadOnlyList<ShikimoriTitle> Animes { get; init; } = [];

    public IReadOnlyList<ShikimoriTitle> Mangas { get; init; } = [];
}
