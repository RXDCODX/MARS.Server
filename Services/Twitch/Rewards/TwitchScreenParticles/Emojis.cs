using MARS.Server.Services.ServiceManager;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Emojis(
    ILogger<Confetty> logger,
    IHubContext<TelegramusHub, ITelegramusHub> hub,
    IHostApplicationLifetime lifetime,
    ITwitchClient client
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "emojis";
    public override string DisplayName => "Emojis";
    public override string Description => "Эмодзи на Twitch";
    public override bool IsServiceActive { get; set; }

    private readonly CancellationToken _token = lifetime.ApplicationStopping;

    private readonly Guid _guid = Guid.Parse("22db3d35-1b76-4674-beb7-cc7546356a84");

    private async void ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (
            !string.IsNullOrWhiteSpace(e.ChatMessage.CustomRewardId)
            && IsServiceActive
            && !TwitchExstension.BlackList.Any(t =>
                t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    var message = e.ChatMessage;

                    if (
                        message.CustomRewardId == _guid.ToString()
                        && message.Channel.Equals(
                            TwitchExstension.Channel,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        await hub.Clients.All.MakeScreenEmojisParticles(message);
                    }
                },
                _token
            );
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                client.OnMessageReceived += ClientOnOnMessageReceived;
            });
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        client.OnMessageReceived -= ClientOnOnMessageReceived;
        return base.StopAsync(cancellationToken);
    }
}
