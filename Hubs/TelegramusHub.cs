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
    SoundBarFactory soundBarFactory,
    WaifuPrizesService waifuPrizesService,
    IServiceProvider serviceProvider,
    MikuMondayTracksService mikuMondayTracksService
) : Hub<ITelegramusHub>
{
    private readonly TwitchConfiguration _twitchConfiguration =
        twitchConfiguration.Value ?? throw new NullReferenceException();

    private float _lastVolumeTts = 0f;

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
        return Task.Run(async () =>
        {
            await soundBarFactory.CreateSoundBar().Unmute();
            try
            {
                var stateManager =
                    serviceProvider.GetRequiredService<MARS.Server.Services.SoundRequest.StateManager>();
                var ttsBroadcaster =
                    serviceProvider.GetRequiredService<MARS.Server.Services.Twitch.Synthesizer.TtsHubBroadcaster>();

                await stateManager.SetMutedAsync(false);
                var state = await stateManager.GetStateAsync();
                if (state.PausedByMute)
                {
                    await stateManager.SetPausedAsync(false);
                    await stateManager.SetPausedByMuteAsync(false);
                }

                var ttsState = new Models.VoiceRecognition.TtsState
                {
                    IsStopped = false,
                    Volume = _lastVolumeTts,
                };
                await ttsBroadcaster.BroadcastStateAsync(ttsState);
            }
            catch
            {
                // ignore
            }
        });
    }

    [SignalRMethod]
    public Task MuteAll(params string[] args)
    {
        return Task.Run(async () =>
        {
            await soundBarFactory.CreateSoundBar().Mute(args);
            try
            {
                var stateManager =
                    serviceProvider.GetRequiredService<MARS.Server.Services.SoundRequest.StateManager>();
                var ttsBroadcaster =
                    serviceProvider.GetRequiredService<MARS.Server.Services.Twitch.Synthesizer.TtsHubBroadcaster>();

                var state = await stateManager.GetStateAsync();
                if (state.State == Services.SoundRequest.Entities.PlaybackState.Playing)
                {
                    await stateManager.SetPausedAsync(true);
                    await stateManager.SetPausedByMuteAsync(true);
                }

                await stateManager.SetMutedAsync(true);

                _lastVolumeTts = state.Volume;
                var ttsState = new Models.VoiceRecognition.TtsState
                {
                    IsStopped = false,
                    Volume = 0.0,
                };
                await ttsBroadcaster.BroadcastStateAsync(ttsState);
            }
            catch
            {
                // ignore
            }
        });
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
