using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Rewards._1702_EmojisReward;

public class Emojis_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Emojis_TwitchReward> logger,
    IHostEnvironment environment,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IHostApplicationLifetime lifetime,
    ITwitchClient client,
    RickRollerService rickRollerService
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "😀 Эмодзи!";

    public override string AlertDescription { get; set; } =
        "🎯 Выстрелить в экран указанными тобой смайликами! Есть поддержка Twitch BTTV 7TV FFZ смайликов! В РФ банят некоторые смайлы, возможно не будет работать 7тв bttv ffz.";

    public override Color Color { get; set; } = Color.FromArgb(31, 255, 72);

    public override int Cost { get; init; } = 1702;

    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    private readonly CancellationToken _token = lifetime.ApplicationStopping;

    private protected override CreateCustomRewardsRequest CreateCustomRewardsRequest
    {
        get
        {
            var values = base.CreateCustomRewardsRequest;
            values.IsUserInputRequired = true;
            return values;
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived += ClientOnOnMessageReceived;
        await base.StartAsync(cancellationToken);
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
                        TwitchRewardId.HasValue
                        && message.CustomRewardId == TwitchRewardId.Value.ToString()
                        && message.Channel.Equals(
                            TwitchExstension.Channel,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        await rickRollerService.TryRickRollAsync(
                            TwitchUser.FromOnMessageReceivedArgs(e)!,
                            () => hubContext.Clients.All.MakeScreenEmojisParticles(message)
                        );
                    }
                },
                _token
            );
        }
    }
}
