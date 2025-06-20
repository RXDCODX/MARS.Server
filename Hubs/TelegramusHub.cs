using MARS.Server.Services.Twitch.SoundBarService;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

[SignalRHub(null, AutoDiscover.MethodsAndParams)]
public class TelegramusHub(
    IDbContextFactory<AppDbContext> factory,
    IOptions<ShikimoriClientOptions> shikiOptions,
    IOptions<TwitchConfiguration> twitchConfiguration,
    ITwitchClient twitchClient,
    SoundBarFactory soundBarFactory
) : Hub<ITelegramusHub>
{
    private readonly TwitchConfiguration _twitchConfiguration =
        twitchConfiguration.Value ?? throw new NullReferenceException();

    private string ShikimoriSite => shikiOptions.Value.ShikimoriSite;

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.PostTwitchInfo(
            _twitchConfiguration.ClientId,
            _twitchConfiguration.ClientSecret
        );
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

    public Task TwitchMsg(string msg)
    {
        return twitchClient.SendMessageToMainTwitchAsync(msg);
    }

    public Task UnmuteSessions()
    {
        return soundBarFactory.CreateSoundBar().Unmute();
    }

    public Task MuteAll(params string[] args)
    {
        return soundBarFactory.CreateSoundBar().Mute(args);
    }
}
