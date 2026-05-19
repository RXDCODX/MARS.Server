using Cyrillic.Convert;
using MARS.Server.Hubs.Models.AudioQuiz;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.Twitch.Entitys.Interfaces;

namespace MARS.Server.Services.Twitch.Rewards._9_AudioQuiz;

public class AudioTriviaMiniGame(
    ITwitchClient client,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IPlayerController playerController,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<AudioTriviaMiniGame> logger
) : ITwitchMiniGame
{
    private const int GameCost = 9;
    private const int RoundSeconds = 30;
    private const double MinSimilarityThreshold = 0.8;

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private CancellationTokenSource _gameTokenSource = new();
    private BaseTrackInfo? _currentTrack;
    private string? _winnerName;
    private bool _shouldResumeSoundRequest;

    public bool IsReuseRewardForAddMechanic { get; set; } = false;
    public bool IsGameRunning { get; set; }
    public string Name => "audiotrivia";

    public int GetGameCost()
    {
        return GameCost;
    }

    public async Task GameStart(
        string userName,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsGameRunning)
        {
            _winnerName = null;
            _shouldResumeSoundRequest = false;
            _currentTrack = await GetRandomTrackAsync(cancellationToken);

            if (_currentTrack != null)
            {
                IsGameRunning = true;
                _gameTokenSource = new CancellationTokenSource();

                await PauseSoundRequestAsync();
                await hubContext.Clients.All.AudioQuizStart(
                    new AudioQuizRoundDto
                    {
                        TrackUrl = _currentTrack.Url.AbsoluteUri,
                        ArtworkUrl = _currentTrack.ArtworkUrl?.AbsoluteUri,
                        RoundSeconds = RoundSeconds,
                    }
                );

                await client.SendMessageToMainTwitchAsync(
                    $"@{userName} запустил(а) аудио викторину! Угадайте трек за {RoundSeconds} секунд",
                    logger
                );

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(RoundSeconds), _gameTokenSource.Token);

                    if (string.IsNullOrWhiteSpace(_winnerName))
                    {
                        await client.SendMessageToMainTwitchAsync(
                            $"Время вышло! Правильный ответ: {_currentTrack.Title}",
                            logger
                        );
                    }
                }
                catch (OperationCanceledException)
                {
                    if (!string.IsNullOrWhiteSpace(_winnerName))
                    {
                        await client.SendMessageToMainTwitchAsync(
                            $"@{_winnerName} победил(а)! Это был трек: {_currentTrack.Title}",
                            logger
                        );
                    }
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                    await client.SendMessageToMainTwitchAsync(
                        "Аудио викторина завершилась с ошибкой",
                        logger
                    );
                }
                finally
                {
                    await EndGameAsync();
                }
            }
            else
            {
                await client.SendMessageToMainTwitchAsync(
                    "Не удалось запустить аудио викторину: в базе нет треков",
                    logger
                );
                IsGameRunning = false;
            }
        }
        else
        {
            await client.SendMessageToMainTwitchAsync("Аудио викторина уже запущена", logger);
        }
    }

    public async Task CancelAsync()
    {
        if (IsGameRunning)
        {
            try
            {
                await _gameTokenSource.CancelAsync();
            }
            catch
            {
                // ignore
            }
        }

        await EndGameAsync();
    }

    public async Task OnChatMessage(string userName, string userId, string message)
    {
        if (IsGameRunning && !string.IsNullOrWhiteSpace(message) && _currentTrack != null)
        {
            var isCorrectAnswer = IsCorrectAnswer(message, _currentTrack);
            if (isCorrectAnswer)
            {
                await _semaphore.WaitAsync();

                try
                {
                    if (IsGameRunning && string.IsNullOrWhiteSpace(_winnerName))
                    {
                        _winnerName = userName;
                        await _gameTokenSource.CancelAsync();
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }
    }

    public Task<bool> OnRewardRedemption(string userName, string userId, int cost)
    {
        return Task.FromResult(false);
    }

    private async Task<BaseTrackInfo?> GetRandomTrackAsync(CancellationToken cancellationToken)
    {
        BaseTrackInfo? result = null;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var totalTracks = await dbContext
            .SoundRequestBaseTrackInfos.AsNoTracking()
            .Where(track => !track.IsDeleted)
            .CountAsync(cancellationToken);

        if (totalTracks > 0)
        {
            var randomIndex = Random.Shared.Next(0, totalTracks);

            result = await dbContext
                .SoundRequestBaseTrackInfos.AsNoTracking()
                .Where(track => !track.IsDeleted)
                .OrderBy(track => track.CreatedAt)
                .Skip(randomIndex)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return result;
    }

    private async Task PauseSoundRequestAsync()
    {
        var state = playerController.GetState();

        if (state.State == PlaybackState.Playing)
        {
            _shouldResumeSoundRequest = true;
            await playerController.PauseAsync(CancellationToken.None);
            await client.SendMessageToMainTwitchAsync(
                "SoundRequest поставлен на паузу на время аудио викторины",
                logger
            );
        }
    }

    private async Task EndGameAsync()
    {
        if (IsGameRunning)
        {
            IsGameRunning = false;
        }

        try
        {
            await hubContext.Clients.All.AudioQuizStop();
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        if (_shouldResumeSoundRequest)
        {
            _shouldResumeSoundRequest = false;

            try
            {
                await playerController.ResumeAsync(CancellationToken.None);
                await client.SendMessageToMainTwitchAsync(
                    "SoundRequest продолжает воспроизведение после аудио викторины",
                    logger
                );
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        _currentTrack = null;
        _winnerName = null;
    }

    private static bool IsCorrectAnswer(string message, BaseTrackInfo track)
    {
        var result = false;

        var messageVariants = BuildComparableVariants(message);
        var expectedVariants = BuildTrackExpectedVariants(track);

        if (messageVariants.Count > 0 && expectedVariants.Count > 0)
        {
            foreach (var messageVariant in messageVariants)
            {
                foreach (var expectedVariant in expectedVariants)
                {
                    if (IsVariantMatch(messageVariant, expectedVariant))
                    {
                        result = true;
                        break;
                    }
                }

                if (result)
                {
                    break;
                }
            }
        }

        return result;
    }

    private static HashSet<string> BuildTrackExpectedVariants(BaseTrackInfo track)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        AddComparableVariants(track.TrackName, result);
        AddComparableVariants(track.Title, result);

        if (track.Authors is { Length: > 0 })
        {
            foreach (var author in track.Authors)
            {
                AddComparableVariants(author, result);
            }

            var joinedAuthors = string.Join(
                ' ',
                track.Authors.Where(a => !string.IsNullOrWhiteSpace(a))
            );
            if (
                !string.IsNullOrWhiteSpace(joinedAuthors)
                && !string.IsNullOrWhiteSpace(track.TrackName)
            )
            {
                AddComparableVariants(string.Concat(joinedAuthors, ' ', track.TrackName), result);
                AddComparableVariants(string.Concat(track.TrackName, ' ', joinedAuthors), result);
            }
        }

        return result;
    }

    private static HashSet<string> BuildComparableVariants(string source)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        AddComparableVariants(source, result);

        return result;
    }

    private static void AddComparableVariants(string? source, HashSet<string> variants)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var normalized = NormalizeText(source);
        if (normalized.Length >= 2)
        {
            variants.Add(normalized);
        }

        // Поддержка NuGet Cyrillic.Convert (транслитерация)
        var cyrillicPackageVariants = ConvertWithCyrillicPackage(source);
        foreach (var cyrillicVariant in cyrillicPackageVariants)
        {
            var normalizedCyrillicVariant = NormalizeText(cyrillicVariant);
            if (normalizedCyrillicVariant.Length >= 2)
            {
                variants.Add(normalizedCyrillicVariant);
            }
        }

        // Поддержка keyboard-layout ошибок (переключатель раскладки)
        var layoutConverted = ConvertKeyboardLayout(source);
        var normalizedLayoutConverted = NormalizeText(layoutConverted);
        if (normalizedLayoutConverted.Length >= 2)
        {
            variants.Add(normalizedLayoutConverted);
        }

        // Комбинированная конвертация на случай конфликта с PuntoSwitcher
        var combinedCyrillicVariants = ConvertWithCyrillicPackage(layoutConverted);
        foreach (var combinedCyrillicVariant in combinedCyrillicVariants)
        {
            var normalizedCombinedVariant = NormalizeText(combinedCyrillicVariant);
            if (normalizedCombinedVariant.Length >= 2)
            {
                variants.Add(normalizedCombinedVariant);
            }
        }
    }

    private static IReadOnlyCollection<string> ConvertWithCyrillicPackage(string value)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(value))
        {
            // В пакете доступны extension-методы для направления Russian<->Latin.
            var toLatin = value.ToRussianLatin();
            if (!string.IsNullOrWhiteSpace(toLatin))
            {
                result.Add(toLatin);
            }

            var toCyrillic = value.ToRussianCyrillic();
            if (!string.IsNullOrWhiteSpace(toCyrillic))
            {
                result.Add(toCyrillic);
            }
        }

        return result;
    }

    private static bool IsVariantMatch(string messageVariant, string expectedVariant)
    {
        var result = false;

        if (
            !string.IsNullOrWhiteSpace(messageVariant)
            && !string.IsNullOrWhiteSpace(expectedVariant)
        )
        {
            if (messageVariant.Contains(expectedVariant, StringComparison.Ordinal))
            {
                result = true;
            }
            else if (expectedVariant.Contains(messageVariant, StringComparison.Ordinal))
            {
                result = true;
            }
            else
            {
                var similarity = CalculateSimilarity(messageVariant, expectedVariant);
                result = similarity >= MinSimilarityThreshold;
            }
        }

        return result;
    }

    private static string ConvertKeyboardLayout(string value)
    {
        var result = value ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(value))
        {
            var lower = value.ToLowerInvariant();
            var latinCount = lower.Count(ch => ch is >= 'a' and <= 'z');
            var cyrillicCount = lower.Count(ch => ch is >= 'а' and <= 'я' || ch == 'ё');

            if (latinCount > 0 && cyrillicCount == 0)
            {
                result = lower.ToRussianCyrillic() ?? string.Empty;
            }
            else if (cyrillicCount > 0 && latinCount == 0)
            {
                result = lower.ToRussianLatin() ?? string.Empty;
            }
        }

        return result;
    }

    private static double CalculateSimilarity(string left, string right)
    {
        var result = 0d;

        if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
        {
            var maxLength = Math.Max(left.Length, right.Length);
            if (maxLength > 0)
            {
                var distance = CalculateLevenshteinDistance(left, right);
                result = 1d - (double)distance / maxLength;
            }
        }

        return result;
    }

    private static int CalculateLevenshteinDistance(string left, string right)
    {
        var result = 0;

        if (string.IsNullOrEmpty(left))
        {
            result = right.Length;
        }
        else if (string.IsNullOrEmpty(right))
        {
            result = left.Length;
        }
        else
        {
            var matrix = new int[left.Length + 1, right.Length + 1];

            for (var i = 0; i <= left.Length; i++)
            {
                matrix[i, 0] = i;
            }

            for (var j = 0; j <= right.Length; j++)
            {
                matrix[0, j] = j;
            }

            for (var i = 1; i <= left.Length; i++)
            {
                for (var j = 1; j <= right.Length; j++)
                {
                    var substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + substitutionCost
                    );
                }
            }

            result = matrix[left.Length, right.Length];
        }

        return result;
    }

    private static string NormalizeText(string value)
    {
        var filtered = value
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch));

        var result = string.Concat(filtered.Where(ch => !char.IsWhiteSpace(ch)));

        return result;
    }
}
