using MARS.Server.Services;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
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
    ILogger<SoundRequestController> logger
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
            
            var message = state.IsPaused ? "Плеер запущен" : "Плеер поставлен на паузу";
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
    /// Установить громкость плеера
    /// </summary>
    [HttpPost("volume/{volume:int}")]
    public async Task<ActionResult<OperationResult>> SetVolume(
        [FromRoute] int volume,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;

        if (volume is >= 0 and <= 100)
        {
            try
            {
                await manager.SetVolume(volume);
                result = Ok(OperationResult.Ok($"Громкость установлена на {volume}%"));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при установке громкости");
                result = Ok(OperationResult.Bad("Ошибка при установке громкости"));
            }
        }
        else
        {
            result = Ok(OperationResult.Bad("Громкость должна быть от 0 до 100"));
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
        [FromBody] AddTrackRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        if (
            !string.IsNullOrWhiteSpace(request.Query)
            && !string.IsNullOrWhiteSpace(request.UserId)
            && !string.IsNullOrWhiteSpace(request.DisplayName)
        )
        {
            try
            {
                var message = await service.AddTrackAsync(
                    request.Query,
                    request.UserId,
                    request.DisplayName,
                    cancellationToken
                );
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
        [FromBody] AddPlaylistRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        if (
            !string.IsNullOrWhiteSpace(request.PlaylistUrl)
            && !string.IsNullOrWhiteSpace(request.UserId)
            && !string.IsNullOrWhiteSpace(request.DisplayName)
        )
        {
            try
            {
                var message = await service.AddPlaylistAsync(
                    request.PlaylistUrl,
                    request.UserId,
                    request.DisplayName,
                    cancellationToken
                );
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
                var message = await service.GetUserQueuePositionAsync(userId);
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

/// <summary>
/// Запрос на добавление трека
/// </summary>
public record AddTrackRequest
{
    /// <summary>
    /// URL видео или поисковый запрос
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// ID пользователя Twitch
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Отображаемое имя пользователя
    /// </summary>
    public required string DisplayName { get; init; }
}

/// <summary>
/// Запрос на добавление плейлиста
/// </summary>
public record AddPlaylistRequest
{
    /// <summary>
    /// URL плейлиста YouTube
    /// </summary>
    public required string PlaylistUrl { get; init; }

    /// <summary>
    /// ID пользователя Twitch
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Отображаемое имя пользователя
    /// </summary>
    public required string DisplayName { get; init; }
}
