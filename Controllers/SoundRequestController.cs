using MARS.Server.Services;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.Twitch;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для получения данных плеера звуковых запросов (очередь, история, состояние)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SoundRequestController(
    MainPlayer player,
    CommandsService service,
    SoundRequestUserQueue queue,
    TwitchUserEnsureService userEnsureService,
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
        CancellationToken _ = default
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
        CancellationToken _ = default
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
    /// Получить историю воспроизведенных треков как элементы очереди
    /// </summary>
    [HttpGet("history/queue-items")]
    public async Task<ActionResult<OperationResult<List<QueueItem>>>> GetHistoryQueueItems(
        [FromQuery] int count = 20,
        CancellationToken _ = default
    )
    {
        ActionResult<OperationResult<List<QueueItem>>> result;

        try
        {
            var historyItems = await player.GetHistoryQueueItemsAsync(count);
            result = Ok(
                OperationResult<List<QueueItem>>.Ok("История получена", historyItems)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении истории как QueueItem");
            result = Ok(
                OperationResult<List<QueueItem>>.Bad("Ошибка при получении истории", [])
            );
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
    /// Удалить элемент из очереди и из БД
    /// </summary>
    [HttpDelete("queue/{queueItemId}")]
    public async Task<ActionResult<OperationResult>> DeleteFromQueue(
        [FromRoute] Guid queueItemId,
        CancellationToken _ = default
    )
    {
        ActionResult<OperationResult> result;

        try
        {
            if (queueItemId != Guid.Empty)
            {
                var queueItem = await queue.GetQueueItemByIdAsync(queueItemId);

                if (queueItem != null)
                {
                    await queue.RemoveFromQueueAsync(queueItemId);
                    logger.LogInformation(
                        "Элемент очереди удален: Id={Id}, Track={Track}",
                        queueItemId,
                        queueItem.Track?.TrackName ?? "null"
                    );
                    result = Ok(OperationResult.Ok("Элемент успешно удален из очереди"));
                }
                else
                {
                    logger.LogWarning(
                        "Попытка удаления несуществующего элемента: Id={Id}",
                        queueItemId
                    );
                    result = Ok(OperationResult.Bad("Элемент не найден в очереди"));
                }
            }
            else
            {
                logger.LogWarning("Попытка удаления элемента с пустым Id");
                result = Ok(OperationResult.Bad("Некорректный идентификатор элемента"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении элемента из очереди: Id={Id}", queueItemId);
            result = Ok(OperationResult.Bad("Ошибка при удалении элемента из очереди"));
        }

        return result;
    }

    /// <summary>
    /// Добавить трек в очередь
    /// </summary>
    /// <param name="query">URL или название трека для поиска</param>
    /// <param name="cancellationToken">Токен отмены</param>
    [HttpPost("add-track")]
    public async Task<ActionResult<OperationResult<string>>> AddTrack(
        [FromQuery] string query,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        try
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                // Убираем префикс !sr если он есть
                var processedQuery = query.Trim();
                if (processedQuery.StartsWith("!sr", StringComparison.OrdinalIgnoreCase))
                {
                    processedQuery = processedQuery.Substring(3).TrimStart();
                    logger.LogInformation(
                        "Удален префикс !sr из запроса: Original={Original}, Processed={Processed}",
                        query,
                        processedQuery
                    );
                }

                // Получаем или создаем пользователя по ChannelId из TwitchExtension
                var channelUser = await userEnsureService.EnsureUserExistsAsync(
                    TwitchExstension.ChannelId,
                    cancellationToken
                );

                // Добавляем трек через сервис
                var message = await service.AddTrackAsync(
                    processedQuery,
                    channelUser,
                    cancellationToken
                );
                logger.LogInformation(
                    "Трек добавлен в очередь: Query={Query}, User={UserDisplayName}, Message={Message}",
                    processedQuery,
                    channelUser.DisplayName,
                    message
                );

                result = Ok(OperationResult<string>.Ok(message, message));
            }
            else
            {
                logger.LogWarning("Попытка добавить трек с пустым query");
                result = Ok(
                    OperationResult<string>.Bad(
                        "Необходимо указать URL или название трека",
                        string.Empty
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при добавлении трека в очередь: Query={Query}", query);
            result = Ok(
                OperationResult<string>.Bad("Ошибка при добавлении трека в очередь", string.Empty)
            );
        }

        return result;
    }

    /// <summary>
    /// Немедленно воспроизвести трек из очереди
    /// Переместить указанный трек на первую позицию и запустить его
    /// Текущий проигрываемый трек перейдёт в историю
    /// </summary>
    /// <param name="queueItemId">ID элемента очереди</param>
    /// <param name="cancellationToken">Токен отмены</param>
    [HttpPost("play-now/{queueItemId}")]
    public async Task<ActionResult<OperationResult<string>>> PlayQueueItemNow(
        [FromRoute] Guid queueItemId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        try
        {
            if (queueItemId != Guid.Empty)
            {
                var message = await service.PlayQueueItemNowAsync(queueItemId, cancellationToken);
                logger.LogInformation(
                    "Попытка немедленного воспроизведения трека: QueueItemId={QueueItemId}, Message={Message}",
                    queueItemId,
                    message
                );

                result = Ok(OperationResult<string>.Ok(message, message));
            }
            else
            {
                logger.LogWarning("Попытка немедленного воспроизведения трека с пустым Id");
                result = Ok(OperationResult<string>.Bad("Некорректный идентификатор элемента", string.Empty));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при немедленном воспроизведении трека: QueueItemId={QueueItemId}",
                queueItemId
            );
            result = Ok(OperationResult<string>.Bad("Ошибка при воспроизведении трека", string.Empty));
        }

        return result;
    }

    public class QueueReorderRequest
    {
        public Guid QueueItemId { get; set; }
        public int NewPosition { get; set; }
    }

    /// <summary>
    /// Поменять позицию элемента в очереди
    /// </summary>
    [HttpPost("queue/reorder")]
    public async Task<ActionResult<OperationResult<string>>> ReorderQueueItem(
        [FromBody] QueueReorderRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<string>> result;

        try
        {
            if (request == null || request.QueueItemId == Guid.Empty)
            {
                result = Ok(OperationResult<string>.Bad("Некорректный запрос", string.Empty));
            }
            else
            {
                var message = await service.ReorderQueueItemAsync(request.QueueItemId, request.NewPosition, cancellationToken);
                logger.LogInformation(
                    "Reorder queue item: Id={Id}, NewPosition={NewPosition}, Message={Message}",
                    request.QueueItemId,
                    request.NewPosition,
                    message
                );

                result = Ok(OperationResult<string>.Ok(message, message));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при перестановке элемента очереди: Id={Id}", request?.QueueItemId);
            result = Ok(OperationResult<string>.Bad("Ошибка при перестановке элемента очереди", string.Empty));
        }

        return result;
    }
}
