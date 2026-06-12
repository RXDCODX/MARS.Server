using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Entitys.Interfaces;
using MARS.Server.Services.Twitch.Rewards._6_RussianRoulette;
using MARS.Server.Services.Twitch.Rewards._7_Quiz;
using MARS.Server.Services.Twitch.Rewards._9_AudioQuiz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards;

public class MiniGamesManager(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ITwitchClient client,
    EventSubWebsocketClient wsClient
) : IHostedService
{
    public bool IsServiceActive { get; set; } = true;

    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;
    private static readonly Dictionary<int, ITwitchMiniGame> MiniGames = [];

    public Task StartAsync(CancellationToken token)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
            client.OnMessageReceived += ClientOnMessageReceived;
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token)
    {
        MiniGames.Clear();
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;
        client.OnMessageReceived -= ClientOnMessageReceived;

        return Task.CompletedTask;
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (!IsServiceActive)
        {
            return;
        }

        var cost = args.Payload.Event.Reward.Cost;
        var name = args.Payload.Event.UserName;
        var userId = args.Payload.Event.UserId;

        // Очищаем завершенные игры перед проверкой
        RemoveCompletedGames();

        var miniGames = MiniGames.Values;
        if (miniGames.Any(e => e.IsGameRunning))
        {
            var gameCost = MiniGames.Keys.FirstOrDefault(e => e == cost, 0);

            if (gameCost != 0)
            {
                var game = MiniGames[gameCost];
                if (!game.IsReuseRewardForAddMechanic)
                {
                    await client.SendMessageToMainTwitchAsync(
                        @$"@{name}, прости но уже другая игра происходит!"
                    );
                }
                else
                {
                    await game.OnRewardRedemption(name, userId, cost);
                }
            }
        }
        else if (cost is 9 or 7 or 6)
        {
            var asyncServiceScope = serviceProvider.CreateAsyncScope();

            switch (cost)
            {
                case 9:
                    var audioTrivia =
                        asyncServiceScope.ServiceProvider.GetRequiredService<AudioTriviaMiniGame>();
                    audioTrivia.IsGameRunning = true;
                    MiniGames.Add(9, audioTrivia);
                    await audioTrivia.GameStart(name, userId, _cancellationToken);
                    break;
                case 7:
                    var twitchTrivia =
                        asyncServiceScope.ServiceProvider.GetRequiredService<TwitchTrivia>();
                    twitchTrivia.IsGameRunning = true;
                    MiniGames.Add(7, twitchTrivia);
                    await twitchTrivia.GameStart(name, userId, _cancellationToken);
                    break;
                case 6:
                    var russianRoulette =
                        asyncServiceScope.ServiceProvider.GetRequiredService<TwitchRussianRoulete>();
                    russianRoulette.IsGameRunning = true;
                    MiniGames.Add(6, russianRoulette);
                    await russianRoulette.GameStart(name, userId, _cancellationToken);
                    break;
            }
        }
    }

    public async Task CancelAllGamesAsync()
    {
        foreach (var game in MiniGames.Values)
        {
            if (game.IsGameRunning)
            {
                await game.CancelAsync();
            }
        }
    }

    public void RemoveCompletedGames()
    {
        var completedGames = MiniGames.Where(kvp => !kvp.Value.IsGameRunning).ToList();
        foreach (var (key, _) in completedGames)
        {
            MiniGames.Remove(key);
        }
    }

    private async Task ClientOnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (
            !IsServiceActive
            || TwitchExstension.BlackList.Logins.Any(t =>
                t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        // Очищаем завершенные игры перед обработкой сообщений
        RemoveCompletedGames();

        var userName = e.ChatMessage.Username;
        var message = e.ChatMessage.Message;
        var userId = e.ChatMessage.UserId;

        foreach (var game in MiniGames.Values.Where(game => game.IsGameRunning))
        {
            await game.OnChatMessage(userName, userId, message);
        }
    }
}
