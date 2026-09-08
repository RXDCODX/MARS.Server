using MARS.Server.Exstensions;
using MARS.Server.Services.SoundBarService.Entitys;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.StreamBotNotifications;

public class TwitchStreamStartupNotifications : IHostedService
{
    private readonly ILogger<TwitchStreamStartupNotifications> _logger;
    private readonly ITwitchClient _twitchClient;
    private readonly EventSubWebsocketClient _wsClient;
    private readonly ISoundBar _soundBar;

    public TwitchStreamStartupNotifications(
        ILogger<TwitchStreamStartupNotifications> logger,
        ITwitchClient twitchClient,
        IHostApplicationLifetime lifetime,
        EventSubWebsocketClient wsClient,
        ISoundBar soundBar
    )
    {
        _logger = logger;
        _twitchClient = twitchClient;
        _wsClient = wsClient;
        _soundBar = soundBar;

        lifetime.ApplicationStarted.Register(() =>
        {
            _wsClient.StreamOffline += PubSibOfflineStream;
            _wsClient.StreamOnline += PubSubOnlineOnStreamUp;
        });
    }

    internal Task PubSubOnlineOnStreamUp(object? sender, StreamOnlineArgs streamOnlineArgs)
    {
        return HandleStreamOnlineAsync();
    }

    internal Task PubSibOfflineStream(object? sender, StreamOfflineArgs args)
    {
        return _twitchClient.SendMessageToMainTwitchAsync(
            "Та куда стрим вырубил Stressed",
            _logger
        );
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task HandleStreamOnlineAsync()
    {
        var audioControllerAvailable = await IsAudioControllerAvailableAsync();

        if (!audioControllerAvailable)
        {
            var reminderMessage =
                "Аудиоконтроллер не запущен. Проверь его запуск, чтобы звуковые запросы работали корректно.";
            await _twitchClient.SendMessageToMainTwitchAsync(reminderMessage, _logger);
        }
    }

    private async Task<bool> IsAudioControllerAvailableAsync()
    {
        var isAvailable = false;

        try
        {
            isAvailable = await _soundBar.CheckHealthAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio controller health-check failed");
        }

        return isAvailable;
    }
}
