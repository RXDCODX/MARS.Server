using MARS.Server.DataBaseContext;
using MARS.Server.Services.WaifuRoll.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/waifu-chat")]
public class WaifuChatController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public WaifuChatController(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    [HttpPost("embeddings")]
    public async Task<IActionResult> StoreEmbedding(
        [FromBody] StoreEmbeddingRequest request,
        CancellationToken ct
    )
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var embedding = new WaifuChatEmbedding
        {
            TwitchId = request.TwitchId,
            Text = request.Text,
            Role = request.Role,
            Embedding = new Vector(request.Embedding),
        };

        db.WaifuChatEmbeddings.Add(embedding);
        await db.SaveChangesAsync(ct);

        return Ok();
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchSimilar(
        [FromBody] SearchRequest request,
        CancellationToken ct
    )
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var embeddingArray = string.Join(",", request.QueryEmbedding.Select(x => x.ToString("F6")));

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(
            $"SELECT \"Id\", \"TwitchId\", \"Text\", \"Role\", \"CreatedAt\", "
                + $"1.0 - (embedding <=> '[{embeddingArray}]'::vector) AS score "
                + $"FROM \"WaifuChatEmbeddings\" "
                + $"WHERE \"TwitchId\" = @twitchId "
                + $"ORDER BY embedding <=> '[{embeddingArray}]'::vector "
                + $"LIMIT {request.TopK}",
            (NpgsqlConnection)conn
        );
        cmd.Parameters.AddWithValue("@twitchId", request.TwitchId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var results = new List<SearchResult>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(
                new SearchResult
                {
                    Text = reader.GetString(2),
                    Role = reader.GetString(3),
                    Score = reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5),
                }
            );
        }

        return Ok(results);
    }

    [HttpPost("facts")]
    public async Task<IActionResult> StoreFact(
        [FromBody] StoreFactRequest request,
        CancellationToken ct
    )
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var fact = new WaifuChatFact
        {
            TwitchId = request.TwitchId,
            Fact = request.Fact,
            Importance = request.Importance,
        };

        db.WaifuChatFacts.Add(fact);
        await db.SaveChangesAsync(ct);

        return Ok();
    }

    [HttpGet("facts/{twitchId}")]
    public async Task<IActionResult> GetFacts(string twitchId, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var facts = await db
            .WaifuChatFacts.Where(f => f.TwitchId == twitchId)
            .OrderByDescending(f => f.Importance)
            .Select(f => f.Fact)
            .ToListAsync(ct);

        return Ok(facts);
    }
}

public class StoreEmbeddingRequest
{
    public required string TwitchId { get; set; }
    public required string Text { get; set; }
    public required string Role { get; set; }
    public required float[] Embedding { get; set; }
}

public class SearchRequest
{
    public required string TwitchId { get; set; }
    public required float[] QueryEmbedding { get; set; }
    public int TopK { get; set; } = 5;
}

public class SearchResult
{
    public required string Text { get; set; }
    public required string Role { get; set; }
    public double Score { get; set; }
}

public class StoreFactRequest
{
    public required string TwitchId { get; set; }
    public required string Fact { get; set; }
    public int Importance { get; set; } = 1;
}
