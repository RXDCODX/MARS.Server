using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.ClientMessages.SignalRAlerts;

public class TwitchMessagesHubAwaker : BackgroundService
{
    private readonly ITwitchClient _client;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly IHostApplicationLifetime _lifetime;

    public TwitchMessagesHubAwaker(
        ITwitchClient client,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IHostApplicationLifetime lifetime
    )
    {
        _client = client;
        _hubContext = hubContext;
        _lifetime = lifetime;
        TwitchMessagesHubAwaker.Instance = client;
    }

    private static ITwitchClient? Instance { get; set; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _lifetime.ApplicationStarted.Register(() =>
        {
            _client.OnMessageReceived += ClientOnOnMessageReceived;

            _client.OnMessageCleared += ClientOnOnMessageCleared;
        });

        return Task.CompletedTask;
    }

    private async void ClientOnOnMessageCleared(object? sender, OnMessageClearedArgs args)
    {
        await Task.Factory.StartNew(
            () => _hubContext.Clients.All.DeleteMessage(args.TargetMessageId)
        );
    }

    private async void ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        await Task.Factory.StartNew(
            () => _hubContext.Clients.All.NewMessage(args.ChatMessage.Id, args.ChatMessage)
        );
    }
}
