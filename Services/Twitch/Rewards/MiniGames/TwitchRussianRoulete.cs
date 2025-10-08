using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Subs;
using TwitchLib.Api.Helix.Models.Chat;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class TwitchRussianRoulete(
    ITwitchClient client,
    ILogger<TwitchRussianRoulete> logger,
    ITwitchAPI api,
    IDbContextFactory<AppDbContext> dbContextFactory,
    TokenService tokenService
) : ITwitchMiniGame, ITwitchReward
{
    public string Name => "russianroulete";
    public bool IsReuseRewardForAddMechanic { get; set; } = true;
    public bool IsGameRunning { get; set; }
    public int Cost { get; init; } = 6;

    private const int MaxPlayers = 50;
    private const double AwaitingTimeForNewPlayersInMilliseconds = 1000 * 60;

    private CancellationTokenSource _cancellationTokenSource = new();
    private DateTimeOffset _gameStartDateTime;
    private bool _gameStillActive;

    private bool _isAwaitingNewPlayers;

    private readonly List<RouletePlayer> _listOfPlayers = [];

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
        return Cost;
    }

    public async Task GameStart(
        string userName,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var name = userName;

        if (!_isAwaitingNewPlayers && !_gameStillActive)
        {
            var listPlayers = new List<RouletePlayer>();
            var seconds = TimeSpan.FromMilliseconds(AwaitingTimeForNewPlayersInMilliseconds);

            var text =
                $"@{name} запускает русскую рулетку, у вас есть {seconds.TotalSeconds} секунд! Чтобы принять участие нажмите на награду за баллы канала стоимостью {Cost}!";
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
            IsGameRunning = true;
            await qwe.RussianRoulette();
            _gameStillActive = false;
        }
    }

    public Task CancelAsync()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _listOfPlayers.Clear();
            _isAwaitingNewPlayers = false;
        }
        finally
        {
            IsGameRunning = false;
        }
        _cancellationTokenSource = new CancellationTokenSource();
        return Task.CompletedTask;
    }

    public Task OnChatMessage(string userName, string userId, string message)
    {
        return Task.CompletedTask;
    }

    public async Task<bool> OnRewardRedemption(string userName, string userId, int cost)
    {
        if (cost == Cost && _isAwaitingNewPlayers && !_gameStillActive)
        {
            if (
                _listOfPlayers.Any(e =>
                    e.TwitchId.Equals(userId, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return false;
            }

            _listOfPlayers.Add(new RouletePlayer { Name = userName, TwitchId = userId });

            // Уведомление о добавлении игрока в рулетку
            await Task.Factory.StartNew(
                async () =>
                    await client.SendMessageToMainTwitchAsync(
                        $"@{userName} присоединился к русской рулетке! Игроков в игре: {_listOfPlayers.Count}",
                        logger
                    )
            );

            return true;
        }

        return false;
    }
}
