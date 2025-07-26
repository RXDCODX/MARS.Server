using MARS.Server.Services.Twitch.Management;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.StreamBotNotifications;

public class TwitchStreamStartupNotifications
{
    private readonly ILogger<TwitchStreamStartupNotifications> _logger;
    private readonly ITwitchClient _twitchClient;
    private readonly EventSubWebsocketClient _wsClient;

    public TwitchStreamStartupNotifications(
        ILogger<TwitchStreamStartupNotifications> logger,
        ITwitchClient twitchClient,
        IHostApplicationLifetime lifetime,
        EventSubWebsocketClient wsClient
    )
    {
        _logger = logger;
        _twitchClient = twitchClient;
        _wsClient = wsClient;

        lifetime.ApplicationStarted.Register(() =>
        {
            _wsClient.StreamOffline += PubSibOfflineStream;
            _wsClient.StreamOnline += PubSubOnlineOnStreamUp;
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
