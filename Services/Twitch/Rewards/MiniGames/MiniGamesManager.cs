using System.Collections.Frozen;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class MiniGamesManager : BackgroundService
{
    private static FrozenDictionary<int, ITwitchMiniGame> _miniGames = FrozenDictionary<
        int,
        ITwitchMiniGame
    >.Empty;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ITwitchClient _client;

    public MiniGamesManager(
        TekkenVictorina tekkenVictorina,
        TwitchRussianRoulete russianRoulete,
        TwitchTrivia twitchTrivia,
        IHostApplicationLifetime lifetime,
        ITwitchClient client
    )
    {
        _lifetime = lifetime;
        _client = client;

        var miniGames = new Dictionary<int, ITwitchMiniGame>
        {
            { tekkenVictorina.GetGameCost(), tekkenVictorina },
            { russianRoulete.GetGameCost(), russianRoulete },
            { twitchTrivia.GetGameCost(), twitchTrivia },
        };
        _miniGames = miniGames.ToFrozenDictionary();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var aa = _lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        return Task.CompletedTask;
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var cost = args.Notification.Payload.Event.Reward.Cost;
        var miniGames = _miniGames.Values;
        var name = args.Notification.Payload.Event.UserName;
        var userId = args.Notification.Payload.Event.UserId;

        if (miniGames.Any(e => e.IsGameRunning))
        {
            await _client.SendMessageToMainTwitchAsync(
                @$"@{name}, прости но уже другая игра происходит!"
            );
            return;
        }
        else
        {
            var gameCost = _miniGames.Keys.FirstOrDefault(e => e == cost, 0);

            if (gameCost != 0)
            {
                var game = _miniGames[gameCost];
                game.IsGameRunning = true;
                await game.GameStart(name, userId);
                return;
            }
        }
    }
}
