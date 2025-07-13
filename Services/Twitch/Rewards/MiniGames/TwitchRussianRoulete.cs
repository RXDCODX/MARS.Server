using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Subs;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class TwitchRussianRoulete(
    ITwitchClient client,
    ILogger<TwitchRussianRoulete> logger,
    ITwitchAPI api,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime applicationLifetime,
    TokenService tokenService
) : ManagedServiceBase(logger), ITwitchMiniGame
{
    public override string ServiceName => "russianroulete";
    public override string DisplayName => "Russian Roulete";
    public override string Description => "Мини-игра Russian Roulete на Twitch";
    public override bool IsServiceActive { get; set; }
    public bool IsReuseRewardForAddMechanic { get; set; } = true;
    public bool IsGameRunning { get; set; }

    private const int MaxPlayers = 50;
    private const double AwaitingTimeForNewPlayersInMilliseconds = 1000 * 60;
    private const int CostOfRoulette = 6;
    private CancellationTokenSource _cancellationTokenSource = new();
    private DateTimeOffset _gameStartDateTime;
    private bool _gameStillActive;

    private bool _isAwaitingNewPlayers;

    private List<RouletePlayer> _listOfPlayers = [];

    private Task Closing(object sender, StreamOfflineArgs args)
    {
        if (!IsGameRunning)
        {
            _cancellationTokenSource.Cancel();
            _listOfPlayers = [];
            _isAwaitingNewPlayers = false;
        }

        IsGameRunning = false;
        return Task.CompletedTask;
    }

    private async Task NewAlert(object sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        var cost = args.Notification.Payload.Event.Reward.Cost;
        var name = args.Notification.Payload.Event.UserName;
        var userId = args.Notification.Payload.Event.UserId;

        if (!IsGameRunning || !IsServiceActive)
        {
            return;
        }

        if (cost == CostOfRoulette && _isAwaitingNewPlayers && !_gameStillActive)
        {
            if (
                _listOfPlayers.Any(e =>
                    e.TwitchId.Equals(userId, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return;
            }

            await client.SendMessageToMainTwitchAsync(
                $"@{name}, ты был добавлен в русскую рулетку!",
                logger
            );
            _listOfPlayers.Add(new RouletePlayer { Name = name, TwitchId = userId });
        }
    }

    private async Task WaitForPlayers()
    {
        var tenSecAuth = false;

        while (_isAwaitingNewPlayers && !_cancellationTokenSource.IsCancellationRequested)
        {
            if (
                DateTimeOffset.Now - _gameStartDateTime
                >= TimeSpan.FromMilliseconds(AwaitingTimeForNewPlayersInMilliseconds)
            )
            {
                _isAwaitingNewPlayers = false;
            }

            if (
                TimeSpan.FromMilliseconds(AwaitingTimeForNewPlayersInMilliseconds)
                    - (DateTimeOffset.Now - _gameStartDateTime)
                    < TimeSpan.FromSeconds(10)
                && !tenSecAuth
            )
            {
                await api.SendAnnouncementToMainTwitch(
                    "Осталось меньше 10 секунд до начала рулетки!",
                    tokenService.Token,
                    AnnouncementColors.Primary,
                    logger
                );
                tenSecAuth = true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);
        }
    }

    public int GetGameCost()
    {
        return CostOfRoulette;
    }

    public async Task GameStart(string userName, string userId)
    {
        var name = userName;

        if (!_isAwaitingNewPlayers && !_gameStillActive)
        {
            var listPlayers = new List<RouletePlayer>();
            var seconds = TimeSpan.FromMilliseconds(AwaitingTimeForNewPlayersInMilliseconds);

            var text =
                $"@{name} запускает русскую рулетку, у вас есть {seconds.TotalSeconds} секунд! Чтобы принять участие нажмите на награду за баллы канала стоимостью {CostOfRoulette}!";
            await api.SendAnnouncementToMainTwitch(
                text,
                tokenService.Token,
                AnnouncementColors.Primary,
                logger
            );
            _gameStartDateTime = DateTimeOffset.Now;
            _isAwaitingNewPlayers = true;

            await WaitForPlayers();

            listPlayers.AddRange(
                [.. _listOfPlayers, new RouletePlayer { Name = name, TwitchId = userId }]
            );
            _listOfPlayers.Clear();

            if (listPlayers.Count > MaxPlayers)
            {
                Console.WriteLine("Ошибка: Слишком много игроков. Максимум " + MaxPlayers);
                return;
            }

            GameType gameType;
            if (listPlayers.Count == 1)
            {
                gameType = GameType.Alone;
            }
            else if (listPlayers.Count == 2)
            {
                gameType = GameType.MiniGame;
            }
            else
            {
                gameType = GameType.Normal;
            }

            var qwe = new RouleteGame(
                listPlayers,
                gameType,
                client,
                logger,
                dbContextFactory,
                this,
                _cancellationTokenSource.Token
            );
            await qwe.RussianRoulette();
            _gameStillActive = false;
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listOfPlayers = [];
        _cancellationTokenSource = new CancellationTokenSource();
        _isAwaitingNewPlayers = false;
        applicationLifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.StreamOffline += Closing;
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd += NewAlert;
        });

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        EventSubService.WsClient.StreamOffline -= Closing;
        EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd -= NewAlert;

        return base.StopAsync(cancellationToken);
    }
}
