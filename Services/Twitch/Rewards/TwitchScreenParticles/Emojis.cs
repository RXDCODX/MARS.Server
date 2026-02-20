using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;

public class Emojis(
    IHubContext<TelegramusHub, ITelegramusHub> hub,
    IHostApplicationLifetime lifetime,
    ITwitchClient client
) : BackgroundService
{
    public bool IsServiceActive { get; set; } = true;

    private readonly CancellationToken _token = lifetime.ApplicationStopping;

    private readonly Guid _guid = Guid.Parse("22db3d35-1b76-4674-beb7-cc7546356a84");

    private async Task ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs e)
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= ClientOnOnMessageReceived;
        await base.StopAsync(cancellationToken);
    }
}
