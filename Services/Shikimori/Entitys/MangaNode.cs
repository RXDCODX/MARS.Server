namespace MARS.Server.Services.Shikimori.Entitys;

public sealed class MangaNode
{
    public long Id { get; init; }

    public string? Name { get; init; }

    public string? Russian { get; init; }

    public string? Description { get; init; }

    public AiredOnNode? AiredOn { get; init; }

    public PosterNode? Poster { get; init; }
}
