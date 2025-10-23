using MARS.Server.Services;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для управления плеером звуковых запросов и очередью
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SoundRequestController(
    MainPlayer player,
    CommandsService service,
    ILogger<SoundRequestController> logger,
    IDbContextFactory<AppDbContext> factory
) : ControllerBase
{
    /// <summary>
    /// Получить состояние плеера
    /// </summary>
    [HttpGet("state")]
    public ActionResult<OperationResult<PlayerState>> GetPlayerState()
    {
        ActionResult<OperationResult<PlayerState>> result;

        try
        {
            var state = player.GetState();
            logger.LogInformation(
                "[GetPlayerState] Состояние плеера: State={State}, CurrentQueueItem={CurrentQueueItem}, NextQueueItem={NextQueueItem}, Volume={Volume}",
                state.State,
                state.CurrentQueueItem?.Track?.TrackName ?? "null",
                state.NextQueueItem?.Track?.TrackName ?? "null",
                state.Volume
            );

            result = Ok(OperationResult<PlayerState>.Ok("Состояние плеера получено", state));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении состояния плеера");
            result = Ok(
                OperationResult<PlayerState>.Bad(
                    "Ошибка при получении состояния плеера",
                    new PlayerState()
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить очередь элементов
    /// </summary>
    [HttpGet("queue")]
    public async Task<ActionResult<OperationResult<List<QueueItem>>>> GetQueue(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<QueueItem>>> result;

        try
        {
            var queue = await player.GetQueueAsync();
            result = Ok(OperationResult<List<QueueItem>>.Ok("Очередь получена", queue));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении очереди");
            result = Ok(OperationResult<List<QueueItem>>.Bad("Ошибка при получении очереди", []));
        }

        return result;
    }

    /// <summary>
    /// Получить историю воспроизведенных треков
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<OperationResult<List<BaseTrackInfo>>>> GetHistory(
        [FromQuery] int count = 20,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<BaseTrackInfo>>> result;

        try
        {
            var history = await player.GetHistoryAsync(count);
            result = Ok(OperationResult<List<BaseTrackInfo>>.Ok("История получена", history));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении истории");
            result = Ok(
                OperationResult<List<BaseTrackInfo>>.Bad("Ошибка при получении истории", [])
            );
        }

        return result;
    }

    /// <summary>
    /// Переключить воспроизведение (Play/Pause)
    /// </summary>
    [HttpPost("toggle-play-pause")]
    public async Task<ActionResult<OperationResult>> PlayOrPause(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            var state = player.GetState();
            await player.TogglePlayPauseAsync();

            var message =
                state.State == PlaybackState.Paused ? "Плеер запущен" : "Плеер поставлен на паузу";
            result = Ok(OperationResult.Ok(message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при переключении воспроизведения");
            result = Ok(OperationResult.Bad("Ошибка при переключении воспроизведения"));
        }

        return result;
    }

    /// <summary>
    /// Остановить плеер
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult<OperationResult>> Stop(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await player.StopAsync(cancellationToken);
            result = Ok(OperationResult.Ok("Плеер остановлен"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при остановке плеера");
            result = Ok(OperationResult.Bad("Ошибка при остановке плеера"));
        }

        return result;
    }

    /// <summary>
    /// Пропустить текущий трек
    /// </summary>
    [HttpPost("skip")]
    public async Task<ActionResult<OperationResult>> Skip(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await player.SkipAsync(cancellationToken);
            result = Ok(OperationResult.Ok("Трек пропущен"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при пропуске трека");
            result = Ok(OperationResult.Bad("Ошибка при пропуске трека"));
        }

        return result;
    }

    /// <summary>
    /// Воспроизвести следующий трек из очереди
    /// </summary>
    [HttpPost("play-next")]
    public async Task<ActionResult<OperationResult>> PlayNext(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await player.PlayNextFromQueueAsync();
            result = Ok(OperationResult.Ok("Следующий трек начал воспроизведение"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при воспроизведении следующего трека");
            result = Ok(OperationResult.Bad("Ошибка при воспроизведении следующего трека"));
        }

        return result;
    }

    /// <summary>
    /// Воспроизвести конкретный элемент из очереди
    /// </summary>
    [HttpPost("play-track/{queueItemId:guid}")]
    public async Task<ActionResult<OperationResult>> PlayTrack(
        [FromRoute] Guid queueItemId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await player.PlayQueueItemAsync(queueItemId);
            result = Ok(OperationResult.Ok("Трек начал воспроизведение"));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при воспроизведении элемента очереди {QueueItemId}",
                queueItemId
            );
            result = Ok(OperationResult.Bad("Ошибка при воспроизведении трека"));
        }

        return result;
    }

    /// <summary>
    /// Включить/выключить звук
    /// </summary>
    [HttpPost("mute/{muted:bool}")]
    public async Task<ActionResult<OperationResult>> SetMute(
        [FromRoute] bool muted,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            if (muted)
            {
                await player.MuteAsync(cancellationToken);
                result = Ok(OperationResult.Ok("Звук выключен"));
            }
            else
            {
                await player.UnmuteAsync(cancellationToken);
                result = Ok(OperationResult.Ok("Звук включен"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при изменении звука");
            result = Ok(OperationResult.Bad("Ошибка при изменении звука"));
        }

        return result;
    }

    /// <summary>
    /// Добавить трек в очередь по URL или поисковому запросу
    /// </summary>
    [HttpPost("add-track")]
    public async Task<ActionResult<OperationResult<string>>> AddTrack(
        [FromQuery] string request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        if (!string.IsNullOrWhiteSpace(request))
        {
            try
            {
                await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
                var user =
                    (
                        await dbContext
                            .TwitchUsers.AsNoTracking()
                            .FirstOrDefaultAsync(
                                e => e.TwitchId == TwitchExstension.ChannelId,
                                cancellationToken: cancellationToken
                            )
                    )
                    ?? new TwitchUser()
                    {
                        TwitchId = TwitchExstension.ChannelId,
                        DisplayName = TwitchExstension.Channel,
                        UserLogin = TwitchExstension.Channel,
                    };

                var message = await service.AddTrackAsync(request, user, cancellationToken);
                result = Ok(OperationResult<string>.Ok(message, message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при добавлении трека");
                result = Ok(
                    OperationResult<string>.Bad("Ошибка при добавлении трека", string.Empty)
                );
            }
        }
        else
        {
            result = Ok(OperationResult<string>.Bad("Неверные параметры запроса", string.Empty));
        }

        return result;
    }

    /// <summary>
    /// Добавить плейлист в очередь
    /// </summary>
    [HttpPost("add-playlist")]
    public async Task<ActionResult<OperationResult<string>>> AddPlaylist(
        [FromQuery] string request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        if (!string.IsNullOrWhiteSpace(request))
        {
            try
            {
                await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
                var user =
                    (
                        await dbContext
                            .TwitchUsers.AsNoTracking()
                            .FirstOrDefaultAsync(
                                e => e.TwitchId == TwitchExstension.ChannelId,
                                cancellationToken: cancellationToken
                            )
                    )
                    ?? new TwitchUser()
                    {
                        TwitchId = TwitchExstension.ChannelId,
                        DisplayName = TwitchExstension.Channel,
                        UserLogin = TwitchExstension.Channel,
                    };

                var message = await service.AddPlaylistAsync(request, user, cancellationToken);
                result = Ok(OperationResult<string>.Ok(message, message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при добавлении плейлиста");
                result = Ok(
                    OperationResult<string>.Bad("Ошибка при добавлении плейлиста", string.Empty)
                );
            }
        }
        else
        {
            result = Ok(OperationResult<string>.Bad("Неверные параметры запроса", string.Empty));
        }

        return result;
    }

    /// <summary>
    /// Удалить элемент из очереди
    /// </summary>
    [HttpDelete("queue/{queueItemId:guid}")]
    public async Task<ActionResult<OperationResult>> RemoveTrack(
        [FromRoute] Guid queueItemId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await player.RemoveQueueItemAsync(queueItemId);
            result = Ok(OperationResult.Ok("Трек удален из очереди"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении элемента очереди {QueueItemId}", queueItemId);
            result = Ok(OperationResult.Bad("Ошибка при удалении трека"));
        }

        return result;
    }

    /// <summary>
    /// Получить текущую или последнюю проигранную песню
    /// </summary>
    [HttpGet("current-song")]
    public async Task<ActionResult<OperationResult<string>>> GetCurrentSong(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        try
        {
            var message = await service.GetCurrentSongAsync(cancellationToken);
            result = Ok(OperationResult<string>.Ok(message, message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении текущей песни");
            result = Ok(
                OperationResult<string>.Bad("Ошибка при получении текущей песни", string.Empty)
            );
        }

        return result;
    }
}
