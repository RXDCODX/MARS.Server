using MARS.Server.Services.TelegramBotService;
using MARS.Server.Services.TelegramPrivateChannelsResender.Entities;
using TL;
using TLDocument = TL.Document;
using TLMessage = TL.Message;
using TLPhotoSize = TL.PhotoSize;

namespace MARS.Server.Services.TelegramPrivateChannelsResender;

/// <summary>
/// Сервис для мониторинга каналов и пересылки медиа контента из forwarded сообщений
/// </summary>
public class TelegramChannelsResenderService(
    ILogger<TelegramChannelsResenderService> logger,
    WTelegramClientService wTelegramClientService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private readonly long[] _monitoredChannels = [-1001803337348, -1001887655244];

    private WTelegramClient? _client;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            _ = InitializeAsync(stoppingToken);
        });

        return Task.CompletedTask;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Инициализация TelegramChannelsResenderService...");

            _client = await wTelegramClientService.GetClientAsync(cancellationToken);

            await InitializeChannelStatesAsync(cancellationToken);

            // Обрабатываем существующие forwarded сообщения в каналах
            await ProcessExistingMessagesAsync(cancellationToken);

            _client.OnUpdates += OnUpdatesReceived;

            logger.LogInformation(
                "TelegramChannelsResenderService успешно инициализирован. Мониторинг каналов: {Channels}",
                string.Join(", ", _monitoredChannels)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при инициализации TelegramChannelsResenderService");
        }
    }

    private async Task ProcessExistingMessagesAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Начинаем полную обработку существующих forwarded сообщений...");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        foreach (var channelId in _monitoredChannels)
        {
            try
            {
                var state = await dbContext.ChannelProcessingStates.FindAsync(
                    [channelId],
                    cancellationToken
                );

                if (state == null)
                {
                    continue;
                }

                var inputPeer = GetInputPeerChannel(channelId);
                if (inputPeer == null)
                {
                    logger.LogWarning(
                        "Не удалось получить InputPeer для канала {ChannelId}",
                        channelId
                    );
                    continue;
                }

                logger.LogInformation(
                    "Начинаем обработку канала {ChannelId} с offset_id: {OffsetId}",
                    channelId,
                    state.OffsetId
                );

                var totalProcessed = 0;
                var hasMoreMessages = true;
                var offsetId = state.OffsetId;

                while (hasMoreMessages && !cancellationToken.IsCancellationRequested)
                {
                    // Получаем батч сообщений используя offset_id пагинацию
                    // Согласно https://core.telegram.org/api/offsets
                    // offset_id - ID сообщения для начала выборки
                    // add_offset - дополнительный offset от offset_id (отрицательное для новых сообщений)
                    // limit - количество сообщений для получения
                    var messages = await _client!.Messages_GetHistory(
                        peer: inputPeer,
                        offset_id: offsetId,
                        add_offset: 0,
                        limit: 100
                    );

                    if (messages is null)
                    {
                        break;
                    }

                    var messagesList = messages.Messages;

                    if (messagesList.Length == 0)
                    {
                        hasMoreMessages = false;
                        break;
                    }

                    var batchProcessedCount = 0;

                    // Сообщения уже отсортированы в порядке убывания ID
                    var sortedMessages = messagesList
                        .OfType<TLMessage>()
                        .OrderBy(m => m.ID)
                        .ToList();

                    foreach (var message in sortedMessages)
                    {
                        // Обрабатываем только forwarded сообщения с медиа
                        if (message is { fwd_from: not null, media: not null })
                        {
                            logger.LogInformation(
                                "Найдено forwarded сообщение {MessageId} в канале {ChannelId} из {Source}",
                                message.ID,
                                channelId,
                                GetForwardSourceInfo(message.fwd_from)
                            );

                            await ProcessForwardedMessageAsync(message, channelId);

                            batchProcessedCount++;
                            totalProcessed++;

                            // Небольшая задержка между обработкой сообщений
                            await Task.Delay(1500, cancellationToken);
                        }

                        // Обновляем offset_id на текущее сообщение
                        offsetId = message.ID;
                    }

                    // Сохраняем прогресс после каждого батча
                    state.OffsetId = offsetId;
                    state.LastUpdated = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Батч обработан: {BatchCount} forwarded сообщений из {TotalInBatch} проверенных. Всего обработано: {Total}",
                        batchProcessedCount,
                        sortedMessages.Count,
                        totalProcessed
                    );

                    // Если в батче меньше 100 сообщений - достигли конца канала
                    if (messagesList.Length < 100)
                    {
                        hasMoreMessages = false;
                    }
                }

                logger.LogInformation(
                    "Завершена обработка канала {ChannelId}. Всего обработано forwarded сообщений: {Total}",
                    channelId,
                    totalProcessed
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка при обработке существующих сообщений в канале {ChannelId}",
                    channelId
                );
            }
        }

        logger.LogInformation("Полная обработка существующих forwarded сообщений завершена");
    }

    private async Task InitializeChannelStatesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        foreach (var channelId in _monitoredChannels)
        {
            var state = await dbContext.ChannelProcessingStates.FindAsync(
                [channelId],
                cancellationToken
            );

            if (state == null)
            {
                state = new ChannelProcessingState
                {
                    ChannelId = channelId,
                    OffsetId = 0,
                    LastUpdated = DateTime.UtcNow,
                };

                dbContext.ChannelProcessingStates.Add(state);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Создано новое состояние для канала {ChannelId}", channelId);
            }
        }
    }

    private async Task OnUpdatesReceived(IObject updates)
    {
        try
        {
            if (updates is not Updates updatesList)
            {
                return;
            }

            foreach (var update in updatesList.UpdateList)
            {
                if (update is UpdateNewChannelMessage channelMessage)
                {
                    await ProcessChannelMessageAsync(channelMessage);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке обновлений");
        }
    }

    private async Task ProcessChannelMessageAsync(UpdateNewChannelMessage update)
    {
        try
        {
            if (update.message is not TLMessage message)
            {
                return;
            }

            var peer = message.Peer;
            var channelId = peer switch
            {
                PeerChannel peerChannel => -1000000000000 - peerChannel.channel_id,
                _ => 0L,
            };

            if (!_monitoredChannels.Contains(channelId))
            {
                return;
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var state = await dbContext.ChannelProcessingStates.FindAsync(channelId);

            if (state == null)
            {
                return;
            }

            logger.LogInformation(
                "Новое сообщение {MessageId} в канале {ChannelId}",
                message.ID,
                channelId
            );

            if (message.fwd_from != null)
            {
                logger.LogInformation(
                    "Обнаружено пересланное сообщение {MessageId} из {SourceInfo}",
                    message.ID,
                    GetForwardSourceInfo(message.fwd_from)
                );

                await ProcessForwardedMessageAsync(message, channelId);
            }

            // Обновляем offset_id для следующей пагинации
            state.OffsetId = message.ID;
            state.LastUpdated = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обработке сообщения {MessageId}",
                update.message is TLMessage msg ? msg.ID : 0
            );
        }
    }

    private async Task ProcessForwardedMessageAsync(TLMessage message, long channelId)
    {
        try
        {
            if (_client == null)
            {
                return;
            }

            if (message.media == null)
            {
                logger.LogInformation(
                    "Сообщение {MessageId} не содержит медиа контента",
                    message.ID
                );
                return;
            }

            logger.LogInformation(
                "Найден медиа контент в сообщении {MessageId}: {MediaType}",
                message.ID,
                message.media.GetType().Name
            );

            var inputPeer = GetInputPeerChannel(channelId);
            if (inputPeer == null)
            {
                logger.LogWarning(
                    "Не удалось получить InputPeer для канала {ChannelId}",
                    channelId
                );
                return;
            }

            // Скачиваем и загружаем медиа заново, чтобы избежать FILE_REFERENCE_EMPTY
            await DownloadAndResendMediaAsync(inputPeer, message);

            await Task.Delay(1000);

            await DeleteMessageAsync(inputPeer, message.ID);

            logger.LogInformation(
                "Медиа из сообщения {MessageId} успешно переслано и оригинал удален",
                message.ID
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обработке пересланного сообщения {MessageId}",
                message.ID
            );
        }
    }

    private async Task DownloadAndResendMediaAsync(InputPeerChannel channel, TLMessage message)
    {
        if (_client == null || message.media == null)
        {
            return;
        }

        try
        {
            switch (message.media)
            {
                case MessageMediaPhoto { photo: Photo photo }:
                    await DownloadAndResendPhotoAsync(channel, photo, message.message);
                    break;

                case MessageMediaDocument { document: TLDocument document }:
                    await DownloadAndResendDocumentAsync(channel, document, message.message);
                    break;

                default:
                    logger.LogWarning(
                        "Неподдерживаемый тип медиа: {MediaType}",
                        message.media.GetType().Name
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при скачивании и загрузке медиа");
            throw;
        }
    }

    private async Task DownloadAndResendPhotoAsync(
        InputPeerChannel channel,
        Photo photo,
        string? caption
    )
    {
        if (_client == null)
        {
            return;
        }

        // Скачиваем фото (самый большой размер)
        if (photo.LargestPhotoSize is not TLPhotoSize largestSize)
        {
            logger.LogWarning("Не удалось найти размер фото {PhotoId}", photo.id);
            return;
        }

        var buffer = new byte[largestSize.FileSize];
        await using var stream = new MemoryStream(buffer);
        var fileType = await _client.DownloadFileAsync(photo, stream, largestSize);

        stream.Position = 0;

        logger.LogInformation(
            "Фото скачано: {Size} bytes, тип: {FileType}",
            stream.Length,
            fileType
        );

        // Загружаем как новый файл
        var inputFile = await _client.UploadFileAsync(stream, $"photo_{photo.id}.jpg");

        // Отправляем
        await _client.Messages_SendMedia(
            peer: channel,
            media: new InputMediaUploadedPhoto { file = inputFile },
            message: caption ?? string.Empty,
            random_id: Random.Shared.NextInt64()
        );

        logger.LogInformation("Фото successfully sent to channel {ChannelId}", channel.channel_id);
    }

    private async Task DownloadAndResendDocumentAsync(
        InputPeerChannel channel,
        TLDocument document,
        string? caption
    )
    {
        if (_client == null)
        {
            return;
        }

        // Скачиваем документ/видео
        var buffer = new byte[document.size];
        await using var stream = new MemoryStream(buffer);
        var fileType = await _client.DownloadFileAsync(document, stream);

        stream.Position = 0;

        logger.LogInformation(
            "Документ скачан: {Size} bytes, тип: {FileType}, mime: {MimeType}",
            stream.Length,
            fileType,
            document.mime_type
        );

        // Определяем имя файла
        var fileName =
            document.attributes.OfType<DocumentAttributeFilename>().FirstOrDefault()?.file_name
            ?? $"file_{document.id}";

        // Загружаем как новый файл
        var inputFile = await _client.UploadFileAsync(stream, fileName);

        // Копируем атрибуты документа
        var attributes = document.attributes.ToArray();

        // Отправляем
        await _client.Messages_SendMedia(
            peer: channel,
            media: new InputMediaUploadedDocument
            {
                file = inputFile,
                mime_type = document.mime_type,
                attributes = attributes,
            },
            message: caption ?? string.Empty,
            random_id: Random.Shared.NextInt64()
        );

        logger.LogInformation("Документ успешно отправлен в канал {ChannelId}", channel.channel_id);
    }

    private async Task DeleteMessageAsync(InputPeerChannel channel, int messageId)
    {
        if (_client == null)
        {
            return;
        }

        await _client.Channels_DeleteMessages(channel, [messageId]);

        logger.LogInformation("Сообщение {MessageId} успешно удалено", messageId);
    }

    private InputPeerChannel? GetInputPeerChannel(long channelId)
    {
        if (_client?.User == null)
        {
            return null;
        }

        var actualChannelId = -channelId - 1000000000000;

        var allChats = _client.Messages_GetAllChats().Result;

        var channel = allChats
            .chats.Values.OfType<Channel>()
            .FirstOrDefault(c => c.id == actualChannelId);

        return channel != null ? new InputPeerChannel(channel.id, channel.access_hash) : null;
    }

    private static string GetForwardSourceInfo(MessageFwdHeader fwdFrom)
    {
        if (fwdFrom.from_id != null)
        {
            return fwdFrom.from_id switch
            {
                PeerChannel peerChannel => $"Channel:{peerChannel.channel_id}",
                PeerUser peerUser => $"User:{peerUser.user_id}",
                PeerChat peerChat => $"Chat:{peerChat.chat_id}",
                _ => "Unknown",
            };
        }

        return !string.IsNullOrEmpty(fwdFrom.from_name)
            ? $"Name:{fwdFrom.from_name}"
            : "Unknown source";
    }

    /// <summary>
    /// Вычисляет хеш сообщений согласно Telegram API для оптимизации повторных запросов
    /// https://core.telegram.org/api/offsets#hash-generation
    /// </summary>
    private static long CalculateMessagesHash(TLMessage[] messages)
    {
        long hash = 0;
        foreach (var message in messages)
        {
            hash = hash ^ (hash >> 21);
            hash = hash ^ (hash << 35);
            hash = hash ^ (hash >> 4);
            hash = hash + message.ID;
        }
        return hash;
    }

    public override void Dispose()
    {
        _client?.OnUpdates -= OnUpdatesReceived;

        base.Dispose();
    }
}
