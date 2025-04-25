using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Subs;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class TwitchRussianRoulete : BackgroundService, ITwitchMiniGame
{
    public bool IsGameRunning { get; set; } = false;

    private const int MaxPlayers = 50;
    private readonly ITwitchAPI _api;
    private readonly double _awaitingTimeForNewPlayersInMilliseconds = 1000 * 60;
    private readonly ITwitchClient _client;
    private readonly TokenService _tokenService;

    private readonly int _costOfRoulette = 6;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<TwitchRussianRoulete> _logger;
    private CancellationTokenSource _cancellationTokenSource;
    private DateTimeOffset _gameStartDateTime;
    private bool _gameStillActive;

    private bool _isAwaitingNewPlayers;
    private bool _isStop = true;

    private List<RouletePlayer> _listOfPlayers = [];

    public TwitchRussianRoulete(
        ITwitchClient client,
        ILogger<TwitchRussianRoulete> logger,
        ITwitchAPI api,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IHostApplicationLifetime applicationLifetime,
        EventSubService eventSubService,
        TokenService tokenService
    )
    {
        _client = client;
        _logger = logger;
        _api = api;
        _dbContextFactory = dbContextFactory;
        _tokenService = tokenService;
        _cancellationTokenSource = new CancellationTokenSource();

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.StreamOffline += Closing;
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd += NewAlert;
        });
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

    private async Task NewAlert(object sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        var cost = args.Notification.Payload.Event.Reward.Cost;
        var name = args.Notification.Payload.Event.UserName;
        var userId = args.Notification.Payload.Event.UserId;

        if (_isStop)
        {
            return;
        }

        if (cost == _costOfRoulette && _isAwaitingNewPlayers && !_gameStillActive)
        {
            if (
                _listOfPlayers.Any(e =>
                    e.TwitchId.Equals(userId, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return;
            }

            await _client.SendMessageToMainTwitchAsync(
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
                await _api.SendAnnouncementToMainTwitch(
                    "Осталось меньше 10 секунд до начала рулетки!",
                    _tokenService.Token,
                    AnnouncementColors.Primary,
                    _logger
                );
                tenSecAuth = true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);
        }
    }

    public int GetGameCost()
    {
        return _costOfRoulette;
    }

    public Task GameStart()
    {
        throw new NotImplementedException();
    }

    public async Task GameStart(string userName, string userId)
    {
        var name = userName;

        if (!_isAwaitingNewPlayers && !_gameStillActive)
        {
            var listPlayers = new List<RouletePlayer>();
            var seconds = TimeSpan.FromMilliseconds(_awaitingTimeForNewPlayersInMilliseconds);

            var text =
                $"@{name} запускает русскую рулетку, у вас есть {seconds.TotalSeconds} секунд! Чтобы принять участие нажмите на награду за баллы канала стоимостью {_costOfRoulette}!";
            await _api.SendAnnouncementToMainTwitch(
                text,
                _tokenService.Token,
                AnnouncementColors.Primary,
                _logger
            );
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
            IsGameRunning = false;
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return InitializeGame();
    }
}
