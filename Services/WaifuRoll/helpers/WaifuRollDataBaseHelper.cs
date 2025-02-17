using MARS.Server.Services.Shikimori;

namespace MARS.Server.Services.WaifuRoll.helpers;

public class WaifuRollDataBaseHelper : ITelegramusService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger _logger;
    private readonly ShikimoriService _shikiService;

    public WaifuRollDataBaseHelper(
        ILogger<WaifuRollDataBaseHelper> logger,
        ShikimoriService shikiService,
        IDbContextFactory<AppDbContext> factory
    )
    {
        _shikiService = shikiService;
        _factory = factory;
        _logger = logger;
    }

    public async Task<Host?> GetRandomPrivatedHost()
    {
        try
        {
            await using var dbContext = await _factory.CreateDbContextAsync();
            var count = dbContext.Hosts.Count(e => !e.IsPrivated);

            if (count > 0)
            {
                var host = await dbContext
                    .Hosts.AsNoTracking()
                    .Include(e => e.HostCoolDown)
                    .Include(e => e.HostGreetings)
                    .Where(e => !e.IsPrivated)
                    .ElementAtAsync(Random.Shared.Next(count));

                return host;
            }

            return null;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return null;
        }
    }

    public async Task<Waifu> EnsureWaifuHaveImageIrl(Waifu waifu)
    {
        if (!string.IsNullOrWhiteSpace(waifu.ImageUrl))
        {
            return waifu;
        }

        var character = await _shikiService.GetShikiCharacterById(waifu.ShikiId);
        waifu.ImageUrl = character!.image.original;
        return waifu;
    }
}
