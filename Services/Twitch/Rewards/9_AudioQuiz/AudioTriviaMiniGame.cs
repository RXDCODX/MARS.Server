using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
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

        var normalizedMessage = NormalizeText(message);
        var normalizedTrackName = NormalizeText(track.TrackName);
        var normalizedTitle = NormalizeText(track.Title);

        if (!string.IsNullOrWhiteSpace(normalizedMessage))
        {
            var trackNameLongEnough = normalizedTrackName.Length >= 4;
            var titleLongEnough = normalizedTitle.Length >= 4;

            if (trackNameLongEnough && normalizedMessage.Contains(normalizedTrackName))
            {
                result = true;
            }
            else if (titleLongEnough && normalizedMessage.Contains(normalizedTitle))
            {
                result = true;
            }
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
