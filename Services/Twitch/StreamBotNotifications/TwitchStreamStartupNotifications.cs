using Hangfire;
using MARS.Server.Services.PyroAlerts;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace MARS.Server.Services.Twitch.StreamBotNotifications;

public class TwitchStreamStartupNotifications
{
    private readonly ILogger<TwitchStreamStartupNotifications> _logger;
    private readonly ITwitchClient _twitchClient;

    public TwitchStreamStartupNotifications(
        ILogger<TwitchStreamStartupNotifications> logger,
        ITelegramBotClient client,
        IDbContextFactory<AppDbContext> factory,
        ITwitchAPI api,
        IWebHostEnvironment environment,
        ITwitchClient twitchClient,
        PyroAlertsHelper helper,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _twitchClient = twitchClient;
    }

    internal Task PubSubOnlineOnStreamUp(object sender, StreamOnlineArgs streamOnlineArgs)
    {
        BackgroundJob.Enqueue(() => _twitchClient.SendMessageToPyrokxnezxzAsync("Online", _logger));
        return Task.CompletedTask;
    }

    internal Task PubSibOfflineStream(object sender, StreamOfflineArgs args)
    {
        BackgroundJob.Enqueue(
            () =>
                _twitchClient.SendMessageToPyrokxnezxzAsync(
                    "Та куда стрим вырубил Stressed",
                    _logger
                )
        );
        return Task.CompletedTask;
    }
}
