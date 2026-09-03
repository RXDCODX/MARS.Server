namespace MARS.Server.Services.Shikimori.Entitys;

public sealed class GraphqlRequest(string query)
{
    public string Query { get; init; } = query;
}
