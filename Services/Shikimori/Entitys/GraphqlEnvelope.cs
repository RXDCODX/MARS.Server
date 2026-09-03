namespace MARS.Server.Services.Shikimori.Entitys;

public sealed class GraphqlEnvelope<TData>
{
    public TData? Data { get; init; }

    public GraphqlError[]? Errors { get; init; }
}
