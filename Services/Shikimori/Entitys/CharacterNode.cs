namespace MARS.Server.Services.Shikimori.Entitys;

public sealed class CharacterNode
{
    public long Id { get; init; }

    public string? Name { get; init; }

    public string? Russian { get; init; }

    public string? Description { get; init; }

    public CharacterImageNode? Image { get; init; }

    public RelatedTitleNode[]? Animes { get; init; }

    public RelatedTitleNode[]? Mangas { get; init; }
}
