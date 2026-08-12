using MARS.Server.DataBaseContext;
using MARS.Server.Services.WaifuRoll.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

public class StoreFactRequest
{
    public required string TwitchId { get; set; }
    public required string Fact { get; set; }
    public int Importance { get; set; } = 1;
}
