using System.Collections.Frozen;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;
using Telegramus.Migrations;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class MiniGamesManager(
    TekkenVictorina tekkenVictorina,
    TwitchRussianRoulete russianRoulete,
    TwitchTrivia twitchTrivia,
    IHostApplicationLifetime lifetime,
    ITwitchClient client,
    ILogger<MiniGamesManager> logger
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "minigames";
    public override string DisplayName => "Mini Games";
    public override string Description => "Менеджер мини-игр Twitch";
    public override bool IsServiceActive { get; set; }

    private static FrozenDictionary<int, ITwitchMiniGame> _miniGames = FrozenDictionary<
        int,
        ITwitchMiniGame
    >.Empty;

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        var miniGames = new Dictionary<int, ITwitchMiniGame>
        {
            { tekkenVictorina.GetGameCost(), tekkenVictorina },
            { russianRoulete.GetGameCost(), russianRoulete },
            { twitchTrivia.GetGameCost(), twitchTrivia },
        };
        _miniGames = miniGames.ToFrozenDictionary();
        lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        _miniGames = FrozenDictionary<int, ITwitchMiniGame>.Empty;
        EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;

        return base.StopAsync(cancellationToken);
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (!IsServiceActive)
        {
            return;
        }

        var cost = args.Notification.Payload.Event.Reward.Cost;
        var miniGames = _miniGames.Values;
        var name = args.Notification.Payload.Event.UserName;
        var userId = args.Notification.Payload.Event.UserId;

        if (miniGames.Any(e => e.IsGameRunning))
        {
            var gameCost = _miniGames.Keys.FirstOrDefault(e => e == cost, 0);

            if (gameCost != 0)
            {
                var game = _miniGames[gameCost];
                if (!game.IsReuseRewardForAddMechanic)
                {
                    await client.SendMessageToMainTwitchAsync(
                        @$"@{name}, прости но уже другая игра происходит!"
                    );
                }
            }
        }
        else
        {
            var gameCost = _miniGames.Keys.FirstOrDefault(e => e == cost, 0);

            if (gameCost != 0)
            {
                var game = _miniGames[gameCost];
                game.IsGameRunning = true;
                await game.GameStart(name, userId);
            }
        }
    }
}
