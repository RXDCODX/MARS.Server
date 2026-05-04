using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards._1702_EmojisReward;

public class Emojis_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Emojis_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IHostApplicationLifetime lifetime,
    ITwitchClient client
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Эмодзи!";

    public override string AlertDescription { get; set; } =
        "Выстрелить в экран указанными тобой смайликами! Есть поддержка Twitch BTTV 7TV FFZ смайликов! В РФ банят некоторые смайлы, возможно не будет работать 7тв bttv ffz.";

    public override Color Color { get; set; } = Color.FromArgb(31, 255, 72);

    public override int Cost { get; init; } = 1702;

    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public bool IsServiceActive { get; set; } = true;

    private readonly CancellationToken _token = lifetime.ApplicationStopping;

    private readonly Guid _guid = Guid.Parse("22db3d35-1b76-4674-beb7-cc7546356a84");

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= ClientOnOnMessageReceived;
        await base.StopAsync(cancellationToken);
    }

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
                        await hubContext.Clients.All.MakeScreenEmojisParticles(message);
                    }
                },
                _token
            );
        }
    }
}
