using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Emojis : BackgroundService
{
    private readonly ILogger<Confetty> _logger;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hub;
    private readonly ITwitchClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly CancellationToken _token;

    private readonly Guid Guid = System.Guid.Parse("22db3d35-1b76-4674-beb7-cc7546356a84");

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

                    if (
                        message.CustomRewardId == Guid.ToString()
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
