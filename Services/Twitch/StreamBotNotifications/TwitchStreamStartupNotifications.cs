using MARS.Server.Services.Twitch.Management;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace MARS.Server.Services.Twitch.StreamBotNotifications;

public class TwitchStreamStartupNotifications
{
    private readonly ILogger<TwitchStreamStartupNotifications> _logger;
    private readonly ITwitchClient _twitchClient;

    public TwitchStreamStartupNotifications(
        ILogger<TwitchStreamStartupNotifications> logger,
        ITwitchClient twitchClient,
        IHostApplicationLifetime lifetime
    )
    {
        _logger = logger;
        _twitchClient = twitchClient;

        lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.StreamOffline += PubSibOfflineStream;
            EventSubService.WsClient.StreamOnline += PubSubOnlineOnStreamUp;
        });
    }

    internal Task PubSubOnlineOnStreamUp(object sender, StreamOnlineArgs streamOnlineArgs)
    {
        return _twitchClient.SendMessageToMainTwitchAsync("Online", _logger);
    }

    internal Task PubSibOfflineStream(object sender, StreamOfflineArgs args)
    {
        return _twitchClient.SendMessageToMainTwitchAsync(
            "Та куда стрим вырубил Stressed",
            _logger
        );
    }
}
