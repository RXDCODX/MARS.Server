namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys;

internal class BullsAndCowsGame(
    string secretCode,
    DateTimeOffset deadline,
    int hintIntervalSeconds,
    int userCooldownSeconds
)
{
    public bool Active { get; set; } = true;
    public string SecretCode { get; } = secretCode;
    public DateTimeOffset Deadline { get; } = deadline;
    private readonly int _hintIntervalSeconds = hintIntervalSeconds;
    private readonly int _userCooldownSeconds = userCooldownSeconds;

    private readonly HashSet<int> _revealedPositions = [];
    private readonly Dictionary<string, DateTimeOffset> _userLastAttempt = [];

    public async Task MainThread(ITwitchClient client, ILogger logger, TwitchBullsAndCows owner)
    {
        try
        {
            while (Active)
            {
                if (DateTimeOffset.Now >= Deadline)
                {
                    Active = false;
                    await client.SendMessageToMainTwitchAsync(
                        $"Время вышло! Код был {SecretCode}.",
                        logger
                    );
                    owner.IsGameRunning = false;
                    break;
                }

                await Task.Delay(_hintIntervalSeconds * 1000, CancellationToken.None);

                if (!Active)
                {
                    break;
                }

                var hint = BuildNextHint();
                if (!string.IsNullOrEmpty(hint))
                {
                    await client.SendMessageToMainTwitchAsync($"Подсказка: {hint}", logger);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
        finally
        {
            owner.IsGameRunning = false;
        }
    }

    public bool CanAcceptAttempt(string userId)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var now = DateTimeOffset.Now;
            if (_userLastAttempt.TryGetValue(userId, out var last))
            {
                var can = now - last >= TimeSpan.FromSeconds(_userCooldownSeconds);
                result = can;
            }
            else
            {
                result = true;
            }
        }

        return result;
    }

    public void RegisterAttempt(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        _userLastAttempt[userId] = DateTimeOffset.Now;
    }

    private string BuildNextHint()
    {
        var result = string.Empty;

        if (_revealedPositions.Count < SecretCode.Length)
        {
            var candidates = Enumerable
                .Range(0, SecretCode.Length)
                .Where(i => !_revealedPositions.Contains(i))
                .ToList();
            if (candidates.Count > 0)
            {
                var index = Random.Shared.Next(candidates.Count);
                var pos = candidates[index];
                _revealedPositions.Add(pos);
                result = $"на позиции {pos + 1} стоит цифра {SecretCode[pos]}";
            }
        }
        else
        {
            var sum = SecretCode.Select(c => c - '0').Sum();
            var evens = SecretCode.Count(c => ((c - '0') % 2) == 0);
            var odds = SecretCode.Length - evens;
            result = $"сумма цифр = {sum}, чётных: {evens}, нечётных: {odds}";
        }

        return result;
    }
}
