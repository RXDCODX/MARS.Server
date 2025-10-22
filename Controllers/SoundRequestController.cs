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
    SoundRequestManager manager,
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
            var state = manager.GetState();
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
    /// Получить очередь треков
    /// </summary>
    [HttpGet("queue")]
    public async Task<ActionResult<OperationResult<List<BaseTrackInfo>>>> GetQueue(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<BaseTrackInfo>>> result;

        try
        {
            var queue = await manager.GetQueueAsync();
            result = Ok(OperationResult<List<BaseTrackInfo>>.Ok("Очередь получена", queue));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении очереди");
            result = Ok(
                OperationResult<List<BaseTrackInfo>>.Bad("Ошибка при получении очереди", [])
            );
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
            var history = await manager.GetHistoryAsync(count);
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
    /// Воспроизвести плеер (Resume или начать воспроизведение следующего трека)
    /// </summary>
    [HttpPost("play")]
    public async Task<ActionResult<OperationResult>> Play(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await manager.PlayAsync();
            result = Ok(OperationResult.Ok("Плеер запущен"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запуске плеера");
            result = Ok(OperationResult.Bad("Ошибка при запуске плеера"));
        }

        return result;
    }

    /// <summary>
    /// Поставить на паузу
    /// </summary>
    [HttpPost("pause")]
    public async Task<ActionResult<OperationResult>> Pause(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await manager.PauseAsync();
            result = Ok(OperationResult.Ok("Плеер поставлен на паузу"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при постановке на паузу");
            result = Ok(OperationResult.Bad("Ошибка при постановке на паузу"));
        }

        return result;
    }

    /// <summary>
    /// Переключить воспроизведение (Play/Pause)
    /// </summary>
    [HttpPost("toggle-play-pause")]
    public async Task<ActionResult<OperationResult>> TogglePlayPause(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            var state = manager.GetState();
            await manager.TogglePlayPauseAsync();

            var message = state.State == PlaybackState.Paused ? "Плеер запущен" : "Плеер поставлен на паузу";
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
            await manager.StopAsync();
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
            await manager.SkipAsync();
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
            await manager.PlayNextFromQueueAsync();
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
    /// Воспроизвести конкретный трек из очереди
    /// </summary>
    [HttpPost("play-track/{trackId:guid}")]
    public async Task<ActionResult<OperationResult>> PlayTrack(
        [FromRoute] Guid trackId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await manager.PlayTrackFromQueueAsync(trackId);
            result = Ok(OperationResult.Ok("Трек начал воспроизведение"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при воспроизведении трека {TrackId}", trackId);
            result = Ok(OperationResult.Bad("Ошибка при воспроизведении трека"));
        }

        return result;
    }

    /// <summary>
    /// Установить громкость плеера (0.0 - 100.0)
    /// </summary>
    [HttpPost("volume/{volume:float}")]
    public async Task<ActionResult<OperationResult>> SetVolume(
        [FromRoute] float volume,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        if (volume is >= 0f and <= 100f)
        {
            try
            {
                await manager.SetVolume(volume);
                result = Ok(OperationResult.Ok($"Громкость установлена на {volume:F1}%"));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при установке громкости");
                result = Ok(OperationResult.Bad("Ошибка при установке громкости"));
            }
        }
        else
        {
            result = Ok(OperationResult.Bad("Громкость должна быть от 0.0 до 100.0"));
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
                await manager.MuteAsync();
                result = Ok(OperationResult.Ok("Звук выключен"));
            }
            else
            {
                await manager.UnmuteAsync();
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
    /// Переключить звук (Mute/Unmute)
    /// </summary>
    [HttpPost("toggle-mute")]
    public async Task<ActionResult<OperationResult>> ToggleMute(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            var state = manager.GetState();
            await manager.ToggleMuteAsync();

            var message = state.IsMuted ? "Звук включен" : "Звук выключен";
            result = Ok(OperationResult.Ok(message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при переключении звука");
            result = Ok(OperationResult.Bad("Ошибка при переключении звука"));
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
    /// Удалить трек из очереди
    /// </summary>
    [HttpDelete("queue/{trackId:guid}")]
    public async Task<ActionResult<OperationResult>> RemoveTrack(
        [FromRoute] Guid trackId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            await manager.RemoveTrack(trackId);
            result = Ok(OperationResult.Ok("Трек удален из очереди"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении трека {TrackId}", trackId);
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

    /// <summary>
    /// Получить позицию пользователя в очереди
    /// </summary>
    [HttpGet("user-position/{userId}")]
    public async Task<ActionResult<OperationResult<string>>> GetUserPosition(
        [FromRoute] string userId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
                var user = await dbContext
                    .TwitchUsers.AsNoTracking()
                    .FirstOrDefaultAsync(
                        e => e.TwitchId == TwitchExstension.ChannelId,
                        cancellationToken: cancellationToken
                    );

                var message = await service.GetUserQueuePositionAsync(user);
                result = Ok(OperationResult<string>.Ok(message, message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при получении позиции пользователя");
                result = Ok(
                    OperationResult<string>.Bad(
                        "Ошибка при получении позиции пользователя",
                        string.Empty
                    )
                );
            }
        }
        else
        {
            result = Ok(OperationResult<string>.Bad("ID пользователя не указан", string.Empty));
        }

        return result;
    }
}
