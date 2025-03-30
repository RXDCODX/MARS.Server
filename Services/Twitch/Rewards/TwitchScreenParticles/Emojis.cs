using MARS.Server.Services.Twitch.Management;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Emojis : BackgroundService
{
    private readonly ILogger<Confetty> _logger;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hub;
    private readonly ITwitchClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly CancellationToken _token;

    private MediaInfo? _alert;

    public Emojis(
        ILogger<Confetty> logger,
        IHubContext<TelegramusHub, ITelegramusHub> hub,
        IHostApplicationLifetime lifetime,
        ITwitchClient client,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _logger = logger;
        _hub = hub;
        _client = client;
        _dbContextFactory = dbContextFactory;
        _token = lifetime.ApplicationStopping;
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
        });
    }

    private async void ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.ChatMessage.CustomRewardId))
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    var message = e.ChatMessage;

                    if (_alert is null)
                    {
                        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                            _token
                        );

                        var alert = dbContext.Alerts.FirstOrDefault(e =>
                            e.MetaInfo.TwitchPointsCost == 1702
                        );

                        _alert = alert ?? throw new NullReferenceException();
                    }

                    if (
                        message.CustomRewardId == _alert.MetaInfo.TwitchGuid.ToString()
                        && message.Channel.Equals(
                            TwitchExstension.Channel,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        await _hub.Clients.All.MakeScreenEmojisParticles(message);
                    }
                },
                _token
            );
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
