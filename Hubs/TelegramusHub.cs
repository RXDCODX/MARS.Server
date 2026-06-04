using MARS.Server.Services.Twitch.Rewards._1580_MikuBeam;
using MARS.Server.Services.Twitch.Rewards._39_MikuMonday;
using MARS.Server.Services.Twitch.SoundBarService;
using MARS.Server.Services.WaifuRoll;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace MARS.Server.Hubs;

[SignalRHub(
    "/hubs/telegramus",
    AutoDiscover.MethodsAndParams,
    null,
    null,
    null,
    false,
    false,
    null,
    HubMethodsScan.Default
)]
public class TelegramusHub(
    IOptions<TwitchConfiguration> twitchConfiguration,
    ITwitchClient twitchClient,
    WaifuPrizesService waifuPrizesService,
    IServiceProvider serviceProvider,
    MikuMondayTracksService mikuMondayTracksService,
    SoundMuteCoordinator coordinator
) : Hub<ITelegramusHub>
{
    private readonly TwitchConfiguration _twitchConfiguration =
        twitchConfiguration.Value ?? throw new NullReferenceException();

    [SwaggerIgnore]
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.PostTwitchInfo(
            _twitchConfiguration.ClientId,
            _twitchConfiguration.ClientSecret
        );
        var result = await waifuPrizesService.GetWaifuPrizesAsync();
        await Clients.Caller.UpdateWaifuPrizes(result.Data);
    }

    [SignalRMethod]
    public Task TwitchMsg(string msg)
    {
        return twitchClient.SendMessageToMainTwitchAsync(msg);
    }

    [SignalRMethod]
    public Task UnmuteSessions()
    {
        return Task.Run(coordinator.UnmuteAsync);
    }

    [SignalRMethod]
    public Task MuteAll(params string[] args)
    {
        return Task.Run(() => coordinator.MuteAsync(args));
    }

    [SignalRMethod]
    public Task MikuMikuDeleteTwitchMessages()
    {
        var mikuBeamService = serviceProvider.GetRequiredService<TwitchMikuBeamRewardService>();
        return mikuBeamService.DeleteMessagesAsync();
    }

    [SignalRMethod]
    public Task ExplosionGo()
    {
        return Clients.All.Explosion();
    }

    [SignalRMethod]
    public Task MikuMondayTracks()
    {
        return mikuMondayTracksService.GetAvailableTracksAsync();
    }
}
