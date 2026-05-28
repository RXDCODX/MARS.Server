using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.EventSub.Core.EventArgs.Stream;

namespace MARS.Server.Services.Twitch.StreamBotNotifications;

public class TwitchStreamStartupNotifications : IHostedService
{
    private readonly ILogger<TwitchStreamStartupNotifications> _logger;
    private readonly ITwitchClient _twitchClient;
    private readonly EventSubWebsocketClient _wsClient;
    private readonly IOptions<HttpClientsConfiguration> _httpClientsConfiguration;
    private readonly IHostEnvironment _environment;

    public TwitchStreamStartupNotifications(
        ILogger<TwitchStreamStartupNotifications> logger,
        ITwitchClient twitchClient,
        IHostApplicationLifetime lifetime,
        EventSubWebsocketClient wsClient,
        IOptions<HttpClientsConfiguration> httpClientsConfiguration,
        IHostEnvironment environment
    )
    {
        _logger = logger;
        _twitchClient = twitchClient;
        _wsClient = wsClient;
        _httpClientsConfiguration = httpClientsConfiguration;
        _environment = environment;

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
        try
        {
            var config = _httpClientsConfiguration.Value;
            var port = _environment.IsProduction()
                ? config.AudioControllerProdPort
                : config.AudioControllerDevPort;
            if (port <= 0)
            {
                port = _environment.IsProduction() ? 30695 : 30691;
            }

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var healthUrl = $"http://127.0.0.1:{port}/api/health";
            using var response = await httpClient.GetAsync(healthUrl);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio controller health-check failed");
            return false;
        }
    }
}
