using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class TwitchBullsAndCows(
    ITwitchClient client,
    ILogger<TwitchBullsAndCows> logger,
    ITwitchAPI api,
    TokenService tokenService
) : ITwitchMiniGame, ITwitchReward
{
    public string Name => "bullsandcows";
    public bool IsReuseRewardForAddMechanic { get; set; } = false;
    public bool IsGameRunning { get; set; }
    public int Cost { get; init; } = 9;

    private const int CodeLength = 4;
    private const int HintIntervalSeconds = 15; // интервал выдачи подсказок
    private const int GameTimeoutSeconds = 120; // общее время игры
    private const int UserCooldownSeconds = 3; // ограничение частоты попыток с пользователя

    private BullsAndCowsGame? _currentGame;
    private readonly SemaphoreSlim _semaphoreSlim = new(1);

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
        if (_currentGame != null)
        {
            await client.SendMessageToMainTwitchAsync("Игра уже запущена!", logger);
            return;
        }

        var code = GenerateSecretCode(CodeLength);
        var now = DateTimeOffset.Now;
        var deadline = now.AddSeconds(GameTimeoutSeconds);
        _currentGame = new BullsAndCowsGame(
            code,
            deadline,
            HintIntervalSeconds,
            UserCooldownSeconds
        );
        IsGameRunning = true;

        var startText =
            $"@{userName} запускает 'Быки и Коровы'! Угадайте {CodeLength}-значный код без повторов за {GameTimeoutSeconds} секунд!";
        await api.SendAnnouncementToMainTwitch(startText, tokenService.Token, null, logger);

        _ = Task.Run(() => _currentGame!.MainThread(client, logger, this), cancellationToken);
    }

    public Task CancelAsync()
    {
        try
        {
            _currentGame?.Active = false;
            _currentGame = null;
        }
        finally
        {
            IsGameRunning = false;
        }

        return Task.CompletedTask;
    }

    public async Task OnChatMessage(string userName, string userId, string message)
    {
        if (!IsGameRunning || _currentGame == null || userName == TwitchExstension.BotName)
        {
            return;
        }

        var normalized = message.Trim();

        if (
            normalized.Equals("!стопкод", StringComparison.OrdinalIgnoreCase)
            && userName.Equals(TwitchExstension.Channel, StringComparison.OrdinalIgnoreCase)
        )
        {
            _currentGame.Active = false;
            _currentGame = null;
            IsGameRunning = false;
            await client.SendMessageToMainTwitchAsync("Игра 'Быки и Коровы' остановлена.", logger);
            return;
        }

        if (!_currentGame.Active)
        {
            return;
        }

        if (!TryParseGuess(normalized, CodeLength, out var guess))
        {
            return;
        }

        await _semaphoreSlim.WaitAsync();
        try
        {
            if (!_currentGame.CanAcceptAttempt(userId))
            {
                return;
            }

            _currentGame.RegisterAttempt(userId);

            var (bulls, cows) = CalculateBullsAndCows(_currentGame.SecretCode, guess);

            if (bulls == CodeLength)
            {
                _currentGame.Active = false;
                await client.SendMessageToMainTwitchAsync(
                    $"Победитель @{userName}! Код был {_currentGame.SecretCode}.",
                    logger
                );
                _currentGame = null;
                IsGameRunning = false;
                return;
            }

            await client.SendMessageToMainTwitchAsync(
                $"@{userName}: Быки:{bulls}, Коровы:{cows}",
                logger
            );
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public Task<bool> OnRewardRedemption(string userName, string userId, int cost)
    {
        return Task.FromResult(false);
    }

    private static string GenerateSecretCode(int length)
    {
        var digits = Enumerable.Range(0, 10).Select(d => (char)('0' + d)).ToList();
        var rnd = Random.Shared;
        var resultChars = new List<char>(length);

        while (resultChars.Count < length)
        {
            var index = rnd.Next(digits.Count);
            var digit = digits[index];

            if (resultChars.Count == 0 && digit == '0')
            {
                continue; // не начинаем с 0
            }

            resultChars.Add(digit);
            digits.RemoveAt(index);
        }

        return new string([.. resultChars]);
    }

    private static (int bulls, int cows) CalculateBullsAndCows(string secret, string guess)
    {
        var bulls = 0;
        var cows = 0;

        for (var i = 0; i < secret.Length; i++)
        {
            if (guess[i] == secret[i])
            {
                bulls++;
            }
            else if (secret.Contains(guess[i]))
            {
                cows++;
            }
        }

        return (bulls, cows);
    }

    private static bool TryParseGuess(string input, int expectedLength, out string guess)
    {
        var result = string.Empty;

        if (!string.IsNullOrWhiteSpace(input))
        {
            var trimmed = input.Trim();
            var isDigitsOnly = trimmed.All(char.IsDigit);
            var isRightLength = trimmed.Length == expectedLength;
            var isAllDistinct = isRightLength && trimmed.Distinct().Count() == expectedLength;

            if (isDigitsOnly && isRightLength && isAllDistinct)
            {
                result = trimmed;
            }
        }

        guess = result;
        return !string.IsNullOrEmpty(result);
    }
}
