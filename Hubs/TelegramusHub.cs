using TwitchLib.Client.Models;

namespace MARS.Server.Hubs;

public class TelegramusHub(
    IDbContextFactory<AppDbContext> factory,
    IOptions<ShikimoriClientOptions> shikiOptions,
    ILogger<TelegramusHub> logger,
    IOptions<TwitchConfiguration> twitchConfiguration,
    ITwitchClient twitchClient
) : Hub<ITelegramusHub>
{
    private readonly TwitchConfiguration _twitchConfiguration =
        twitchConfiguration.Value ?? throw new NullReferenceException();
    private string ShikimoriSite => shikiOptions.Value.ShikimoriSite;

    public override async Task OnConnectedAsync()
    {
        await UpdateWaifuPrizesAsync();
        await Clients.Caller.PostTwitchInfo(
            _twitchConfiguration.ClientId,
            _twitchConfiguration.ClientSecret
        );
    }

    public Task SendNewMessage(string id, ChatMessage content)
    {
        return Clients.All.NewMessage(id, content);
    }

    public Task DeleteMessage(string id)
    {
        return Clients.All.DeleteMessage(id);
    }

    public Task TwitchMsg(string msg)
    {
        return twitchClient.SendMessageToPyrokxnezxzAsync(msg, logger);
    }

    public async Task UpdateWaifuPrizesAsync()
    {
        var prizes = await GetWaifuPrizesAsync();
        await Clients.Caller.UpdateWaifuPrizes(prizes);
    }

    private async Task<ICollection<PrizeType>> GetWaifuPrizesAsync(
        AppDbContext? dbContext = default
    )
    {
        dbContext ??= await factory.CreateDbContextAsync();
        var prizes = await dbContext
            .Waifus.AsNoTracking()
            .Where(e => true)
            .Select(e => new PrizeType()
            {
                Id = e.ShikiId,
                Image = ShikimoriSite + "/" + e.ImageUrl,
                Text = e.Name,
            })
            .ToListAsync();
        return prizes;
    }
}
