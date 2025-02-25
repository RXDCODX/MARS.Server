using Hangfire;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Subs;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class TwitchRussianRoulete : BackgroundService
{
    private const int MaxPlayers = 50;
    private readonly ITwitchAPI _api;
    private readonly double _awaitingTimeForNewPlayersInMilliseconds = 1000 * 60;
    private readonly ITwitchClient _client;

    private readonly int _costOfRoulette = 6;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly UserAuthHelper _helper;
    private readonly ILogger<TwitchRussianRoulete> _logger;
    private CancellationTokenSource _cancellationTokenSource;
    private DateTimeOffset _gameStartDateTime;
    private bool _gameStillActive;

    private bool _isAwaitingNewPlayers;
    private bool _isStop = true;

    private List<RouletePlayer> _listOfPlayers = new();

    public TwitchRussianRoulete(
        ITwitchClient client,
        ILogger<TwitchRussianRoulete> logger,
        UserAuthHelper helper,
        ITwitchAPI api,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _client = client;
        _logger = logger;
        _helper = helper;
        _api = api;
        _dbContextFactory = dbContextFactory;
        _cancellationTokenSource = new CancellationTokenSource();

        UserAuthHelper.WsClient.StreamOffline += Closing;
        UserAuthHelper.WsClient.ChannelPointsCustomRewardRedemptionAdd += NewAlert;
    }

    private Task InitializeGame()
    {
        _listOfPlayers = new List<RouletePlayer>();
        _isStop = false;
        _cancellationTokenSource = new CancellationTokenSource();
        _isAwaitingNewPlayers = false;

        return Task.CompletedTask;
    }

    private Task Closing(object sender, StreamOfflineArgs args)
    {
        if (!_isStop)
        {
            _cancellationTokenSource.Cancel();
            _listOfPlayers = new List<RouletePlayer>();
            _isAwaitingNewPlayers = false;
        }

        _isStop = true;
        return Task.CompletedTask;
    }

    private Task NewAlert(object sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        BackgroundJob.Enqueue(() => Process(args));

        return Task.CompletedTask;
    }

    public async Task Process(ChannelPointsCustomRewardRedemptionArgs args)
    {
        var cost = args.Notification.Payload.Event.Reward.Cost;
        var name = args.Notification.Payload.Event.UserName;
        var userId = args.Notification.Payload.Event.UserId;

        if (_isStop)
        {
            return;
        }

        if (cost == _costOfRoulette && !_isAwaitingNewPlayers && !_gameStillActive)
        {
            var listPlayers = new List<RouletePlayer>();
            var seconds = TimeSpan.FromMilliseconds(_awaitingTimeForNewPlayersInMilliseconds);

            var text =
                $"@{name} запускает русскую рулетку, у вас есть {seconds.TotalSeconds} секунд! Чтобы принять участие нажмите на награду за баллы канала стоимостью {_costOfRoulette}!";
            await _api.Helix.Chat.SendChatAnnouncementAsync(
                TwitchExstension.ChannelId,
                TwitchExstension.ChannelId,
                text,
                AnnouncementColors.Primary,
                _helper.Token!.AccessToken
            );
            //_client.Announce(TwitchExstension.Channel, $"@{name} запускает русскую рулетку, у вас есть {seconds.TotalSeconds} секунд! Чтобы принять участие нажмите на награду за баллы канала стоимостью {_costOfRoulette}!");
            _gameStartDateTime = DateTimeOffset.Now;
            _isAwaitingNewPlayers = true;

            await WaitForPlayers();

            listPlayers.Add(new RouletePlayer { Name = name, TwitchId = userId });

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
                _cancellationTokenSource.Token,
                _client,
                _logger,
                _dbContextFactory
            );
            await qwe.RussianRoulette();
            _gameStillActive = false;
        }
        else if (cost == _costOfRoulette && _isAwaitingNewPlayers && !_gameStillActive)
        {
            if (
                _listOfPlayers.Any(e =>
                    e.TwitchId.Equals(userId, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return;
            }

            await _client.SendMessageToPyrokxnezxzAsync(
                $"@{name}, ты был добавлен в русскую рулетку!",
                _logger
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
                >= TimeSpan.FromMilliseconds(_awaitingTimeForNewPlayersInMilliseconds)
            )
            {
                _listOfPlayers.Clear();
                _isAwaitingNewPlayers = false;
            }

            if (
                TimeSpan.FromMilliseconds(_awaitingTimeForNewPlayersInMilliseconds)
                    - (DateTimeOffset.Now - _gameStartDateTime)
                    < TimeSpan.FromSeconds(10)
                && !tenSecAuth
            )
            {
                await _api.Helix.Chat.SendChatAnnouncementAsync(
                    TwitchExstension.ChannelId,
                    TwitchExstension.ChannelId,
                    "Осталось меньше 10 секунд до начала рулетки!",
                    AnnouncementColors.Primary,
                    _helper.Token!.AccessToken
                );
                tenSecAuth = true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return InitializeGame();
    }
}
