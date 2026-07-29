using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.Telegram.PrivateChannelsResender.Entities;
using MARS.Server.Services.Telegram.WTelegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TL;
using InputMediaType = TL.InputMedia;
using TLDocument = TL.Document;
using TLMessage = TL.Message;
using TLPhotoSize = TL.PhotoSize;

namespace MARS.Server.Services.Telegram.PrivateChannelsResender;

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

    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        long,
        string
    > _channelTitleCache = new();

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
                var maxId = 0;
                var processedGroupIds = new HashSet<long>();

                while (hasMoreMessages && !cancellationToken.IsCancellationRequested)
                {
                    // Получаем батч сообщений используя offset_id пагинацию
                    // Согласно https://core.telegram.org/api/offsets
                    // offset_id - ID сообщения для начала выборки
                    // add_offset - дополнительный offset от offset_id (отрицательное для новых сообщений)
                    // limit - количество сообщений для получения
                    var messages = await _client!.Messages_GetHistory(
                        peer: inputPeer,
                        offset_id: offsetId + 100,
                        add_offset: 0,
                        limit: 100
                    );

                    if (messages is null)
                    {
                        break;
                    }

                    var messagesList = messages.Messages;

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

                            // Пропускаем, если уже обработали эту группу
                            if (
                                message.grouped_id != 0
                                && processedGroupIds.Contains(message.grouped_id)
                            )
                            {
                                continue;
                            }

                            await ProcessForwardedMessageAsync(message, channelId);

                            if (message.grouped_id != 0)
                            {
                                processedGroupIds.Add(message.grouped_id);
                            }

                            batchProcessedCount++;
                            totalProcessed++;

                            // Небольшая задержка между обработкой сообщений
                            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                        }

                        // Обновляем offset_id на текущее сообщение
                        offsetId = message.ID;
                    }

                    // Сохраняем прогресс после каждого батча
                    state.OffsetId = offsetId;
                    state.LastUpdated = DateTime.Now;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    if (maxId == (messagesList.MaxBy(e => e.ID)?.ID ?? 0))
                    {
                        hasMoreMessages = false;
                        break;
                    }
                    else
                    {
                        maxId = messagesList.MaxBy(e => e.ID)?.ID ?? 0;
                    }

                    logger.LogInformation(
                        "Батч обработан: {BatchCount} forwarded сообщений из {TotalInBatch} проверенных. Всего обработано: {Total}",
                        batchProcessedCount,
                        sortedMessages.Count,
                        totalProcessed
                    );
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
                    LastUpdated = DateTime.Now,
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

            if (!Enumerable.Contains(_monitoredChannels, channelId))
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
            state.LastUpdated = DateTime.Now;
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

            // Проверяем, является ли это частью альбома (группировка медиа)
            if (message.grouped_id != 0)
            {
                await ProcessGroupedMediaAsync(message, channelId);
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
                "Медия из сообщения {MessageId} успешно переслано и оригинал удален",
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

    private async Task ProcessGroupedMediaAsync(TLMessage originalMessage, long channelId)
    {
        try
        {
            var inputPeer = GetInputPeerChannel(channelId);
            if (inputPeer == null)
            {
                logger.LogWarning(
                    "Не удалось получить InputPeer для канала {ChannelId}",
                    channelId
                );
                return;
            }

            // Собираем все сообщения в группе с одинаковым grouped_id
            var groupedMessages = await FetchGroupedMessagesAsync(
                inputPeer,
                originalMessage.grouped_id,
                originalMessage.ID
            );

            if (groupedMessages.Count == 0)
            {
                logger.LogWarning(
                    "Не удалось найти сообщения группы для grouped_id {GroupedId} (messageId: {MessageId})",
                    originalMessage.grouped_id,
                    originalMessage.ID
                );
                return;
            }

            logger.LogInformation(
                "Обнаружена группа медиа с {Count} элементами (grouped_id: {GroupedId})",
                groupedMessages.Count,
                originalMessage.grouped_id
            );

            // Подготавливаем медиа для альбома
            var inputMedias = new List<InputMediaType>();

            foreach (var msg in groupedMessages)
            {
                if (msg.media != null)
                {
                    var inputMedia = await PrepareMediaForAlbumAsync(msg.media);
                    if (inputMedia != null)
                    {
                        inputMedias.Add(inputMedia);
                    }
                }
            }

            if (inputMedias.Count == 0)
            {
                logger.LogWarning("Не удалось подготовить медиа для отправки");
                return;
            }

            // Формируем подпись для альбома
            var caption = FormatGroupedCaption(originalMessage);

            // Отправляем альбом одним сообщением
            await _client!.SendAlbumAsync(inputPeer, inputMedias, caption);

            logger.LogInformation("Альбом из {Count} медиа успешно отправлен", inputMedias.Count);

            // Удаляем оригинальные сообщения
            var messageIds = groupedMessages.Select(m => m.ID).ToArray();
            await DeleteMessagesAsync(inputPeer, messageIds);

            // Помечаем диалог как непрочитанный
            try
            {
                await _client.Messages_MarkDialogUnread(inputPeer, unread: true);
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Не удалось пометить диалог как непрочитанный для канала {ChannelId}",
                    channelId
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обработке группировки медиа (grouped_id: {GroupedId})",
                originalMessage.grouped_id
            );
        }
    }

    private async Task<InputMediaType?> PrepareMediaForAlbumAsync(MessageMedia media)
    {
        if (_client == null)
        {
            return null;
        }

        try
        {
            return media switch
            {
                MessageMediaPhoto { photo: Photo photo } => await PreparePhotoForAlbumAsync(photo),
                MessageMediaDocument { document: TLDocument document } =>
                    await PrepareDocumentForAlbumAsync(document),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подготовке медиа для альбома");
            return null;
        }
    }

    private async Task<InputMediaType?> PreparePhotoForAlbumAsync(Photo photo)
    {
        if (_client == null)
        {
            return null;
        }

        try
        {
            // PhotoBase имеет implicit conversion к InputMediaPhoto
            if (photo is PhotoBase photoBase)
            {
                return photoBase;
            }

            // Для других типов фото - загружаем
            var largestSize =
                photo.LargestPhotoSize as TLPhotoSize
                ?? photo
                    .sizes?.OfType<TLPhotoSize>()
                    .OrderByDescending(s => s.FileSize)
                    .FirstOrDefault();

            if (largestSize == null)
            {
                logger.LogWarning("Не удалось найти пригодный размер фото {PhotoId}", photo.id);
                return null;
            }

            var buffer = new byte[Math.Max(1, largestSize.FileSize)];
            await using var stream = new MemoryStream(buffer);
            var fileType = await _client.DownloadFileAsync(photo, stream, largestSize);

            stream.Position = 0;

            var extension = GetExtensionForPhoto(fileType);
            var inputFile = await _client.UploadFileAsync(stream, $"photo_{photo.id}{extension}");

            return new InputMediaUploadedPhoto { file = inputFile };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подготовке фото для альбома");
            return null;
        }
    }

    private async Task<InputMediaType?> PrepareDocumentForAlbumAsync(TLDocument document)
    {
        if (_client == null)
        {
            return null;
        }

        try
        {
            var buffer = new byte[Math.Max(1, document.size)];
            await using var stream = new MemoryStream(buffer);
            await _client.DownloadFileAsync(document, stream);

            stream.Position = 0;

            var fileName = document
                .attributes.OfType<DocumentAttributeFilename>()
                .FirstOrDefault()
                ?.file_name;

            if (string.IsNullOrEmpty(fileName))
            {
                var extension = GetExtensionFromMimeType(document.mime_type);
                fileName = $"file_{document.id}{extension}";
            }
            else
            {
                var extension = GetExtensionFromMimeType(document.mime_type);
                if (
                    !string.IsNullOrEmpty(extension)
                    && !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                )
                {
                    fileName += extension;
                }
            }

            var inputFile = await _client.UploadFileAsync(stream, fileName);
            var attributes = document.attributes.ToArray();

            return new InputMediaUploadedDocument
            {
                file = inputFile,
                mime_type = document.mime_type,
                attributes = attributes,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подготовке документа для альбома");
            return null;
        }
    }

    private string FormatGroupedCaption(TLMessage originalMessage)
    {
        var originalCaption = string.IsNullOrWhiteSpace(originalMessage.message)
            ? string.Empty
            : originalMessage.message.Trim();

        var timestamp = originalMessage.Date.ToLocalTime();
        var sourceName = "Unknown";
        if (originalMessage.fwd_from != null)
        {
            sourceName = !string.IsNullOrEmpty(originalMessage.fwd_from.from_name)
                ? originalMessage.fwd_from.from_name
                : GetForwardSourceInfo(originalMessage.fwd_from);
        }

        var meta =
            $"(id: {originalMessage.Peer.ID}, from: {sourceName}, date: {timestamp:yyyy-MM-dd HH:mm})";

        return string.IsNullOrEmpty(originalCaption) ? meta : $"{originalCaption}\n{meta}";
    }

    private async Task DeleteMessagesAsync(InputPeerChannel channel, int[] messageIds)
    {
        if (_client == null || messageIds.Length == 0)
        {
            return;
        }

        await _client.Channels_DeleteMessages(channel, messageIds);

        logger.LogInformation("Удалено {Count} сообщений из группы", messageIds.Length);
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
                    await DownloadAndResendPhotoAsync(channel, photo, message);
                    break;

                case MessageMediaDocument { document: TLDocument document }:
                    await DownloadAndResendDocumentAsync(channel, document, message);
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
        TLMessage originalMessage
    )
    {
        if (_client == null)
        {
            return;
        }

        // Попытка получить TLPhotoSize: сначала из LargestPhotoSize, иначе из списка sizes
        var largestSize =
            photo.LargestPhotoSize as TLPhotoSize
            ?? photo
                .sizes?.OfType<TLPhotoSize>()
                .OrderByDescending(s => s.FileSize)
                .FirstOrDefault();

        if (largestSize == null)
        {
            logger.LogWarning("Не удалось найти пригодный размер фото {PhotoId}", photo.id);
            return;
        }

        var buffer = new byte[Math.Max(1, largestSize.FileSize)];
        await using var stream = new MemoryStream(buffer);
        var fileType = await _client.DownloadFileAsync(photo, stream, largestSize);

        stream.Position = 0;

        logger.LogInformation(
            "Фото скачано: {Size} bytes, тип: {FileType}",
            stream.Length,
            fileType
        );

        var extension = GetExtensionForPhoto(fileType);
        // Загружаем как новый файл
        var inputFile = await _client.UploadFileAsync(stream, $"photo_{photo.id}{extension}");

        // Формируем подпись: оригинальная подпись + метаданные (id, источник, дата)
        var originalCaption = string.IsNullOrWhiteSpace(originalMessage.message)
            ? string.Empty
            : originalMessage.message.Trim();
        var timestamp = originalMessage.Date.ToLocalTime();
        var sourceName = "Unknown";
        if (originalMessage.fwd_from != null)
        {
            sourceName = !string.IsNullOrEmpty(originalMessage.fwd_from.from_name)
                ? originalMessage.fwd_from.from_name
                : GetForwardSourceInfo(originalMessage.fwd_from);
        }
        var meta =
            $"(id: {originalMessage.Peer.ID}, from: {sourceName}, date: {timestamp:yyyy-MM-dd HH:mm})";
        var captionWithMeta = string.IsNullOrEmpty(originalCaption)
            ? meta
            : $"{originalCaption}\n{meta}";

        // Отправляем
        await _client.Messages_SendMedia(
            peer: channel,
            media: new InputMediaUploadedPhoto { file = inputFile },
            message: captionWithMeta,
            random_id: Random.Shared.NextInt64()
        );

        logger.LogInformation("Фото успешно отправлено в канал {ChannelId}", channel.channel_id);

        // После отправки помечаем диалог как непрочитанный для текущего пользователя (кеш/лог)
        try
        {
            await _client.Messages_MarkDialogUnread(channel, unread: true);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Не удалось пометить диалог как непрочитанный для канала {ChannelId}",
                channel.channel_id
            );
        }
    }

    private async Task DownloadAndResendDocumentAsync(
        InputPeerChannel channel,
        TLDocument document,
        TLMessage originalMessage
    )
    {
        if (_client == null)
        {
            return;
        }

        // Скачиваем документ/видео
        var buffer = new byte[Math.Max(1, document.size)];
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
        var fileName = document
            .attributes.OfType<DocumentAttributeFilename>()
            .FirstOrDefault()
            ?.file_name;

        if (string.IsNullOrEmpty(fileName))
        {
            var extension = GetExtensionFromMimeType(document.mime_type);
            fileName = $"file_{document.id}{extension}";
        }
        else
        {
            var extension = GetExtensionFromMimeType(document.mime_type);
            if (
                !string.IsNullOrEmpty(extension)
                && !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            )
            {
                fileName += extension;
            }
        }

        // Загружаем как новый файл
        var inputFile = await _client.UploadFileAsync(stream, fileName);

        // Копируем атрибуты документа
        var attributes = document.attributes.ToArray();

        // Формируем подпись: оригинальная подпись + метаданные (id, источник, дата)
        var originalCaption = string.IsNullOrWhiteSpace(originalMessage.message)
            ? string.Empty
            : originalMessage.message.Trim();
        var timestamp = originalMessage.date.ToLocalTime();
        var sourceName = "Unknown";
        if (originalMessage.fwd_from != null)
        {
            sourceName = !string.IsNullOrEmpty(originalMessage.fwd_from.from_name)
                ? originalMessage.fwd_from.from_name
                : GetForwardSourceInfo(originalMessage.fwd_from);
        }
        var metaDoc =
            $"(id: {originalMessage.Peer.ID}, from: {sourceName}, date: {timestamp:yyyy-MM-dd HH:mm})";
        var captionWithMetaDoc = string.IsNullOrEmpty(originalCaption)
            ? metaDoc
            : $"{originalCaption}\n{metaDoc}";

        // Отправляем
        await _client.Messages_SendMedia(
            peer: channel,
            media: new InputMediaUploadedDocument
            {
                file = inputFile,
                mime_type = document.mime_type,
                attributes = attributes,
            },
            message: captionWithMetaDoc,
            random_id: Random.Shared.NextInt64()
        );

        logger.LogInformation("Документ успешно отправлен в канал {ChannelId}", channel.channel_id);

        // После отправки помечаем диалог как непрочитанный для текущего пользователя (кеш/лог)
        try
        {
            await _client.Messages_MarkDialogUnread(channel, unread: true);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Не удалось пометить диалог как непрочитанный для канала {ChannelId}",
                channel.channel_id
            );
        }
    }

    private async Task DeleteMessageAsync(InputPeerChannel channel, int messageId)
    {
        if (_client == null)
        {
            return;
        }

        await _client.Channels_DeleteMessages(channel, new[] { messageId });

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

    private string GetChannelTitle(long peerChannelId)
    {
        if (_client == null)
        {
            return $"Channel:{peerChannelId}";
        }

        if (_channelTitleCache.TryGetValue(peerChannelId, out var cached))
        {
            return cached;
        }

        try
        {
            // Messages_GetAllChats may be sync; use Result to reuse existing client API
            var allChats = _client.Messages_GetAllChats().Result;
            var channel = allChats
                .chats.Values.OfType<Channel>()
                .FirstOrDefault(c => c.id == peerChannelId);
            if (channel != null)
            {
                var title = channel.title ?? $"Channel:{peerChannelId}";
                _channelTitleCache.TryAdd(peerChannelId, title);
                return title;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Не удалось получить список чатов для поиска названия канала {ChannelId}",
                peerChannelId
            );
        }

        var fallback = $"Channel:{peerChannelId}";
        _channelTitleCache.TryAdd(peerChannelId, fallback);
        return fallback;
    }

    private string GetForwardSourceInfo(MessageFwdHeader fwdFrom)
    {
        if (fwdFrom.from_id != null)
        {
            if (fwdFrom.from_id is PeerChannel peerChannel)
            {
                // peerChannel.channel_id is the numeric id of the source channel
                return GetChannelTitle(peerChannel.channel_id);
            }

            if (fwdFrom.from_id is PeerUser peerUser)
            {
                return $"User:{peerUser.user_id}";
            }

            if (fwdFrom.from_id is PeerChat peerChat)
            {
                return $"Chat:{peerChat.chat_id}";
            }

            return "Unknown";
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
            hash ^= (hash >> 21);
            hash ^= (hash << 35);
            hash ^= (hash >> 4);
            hash += message.ID;
        }
        return hash;
    }

    public override void Dispose()
    {
        _client?.OnUpdates -= OnUpdatesReceived;

        base.Dispose();
    }

    public static string GetExtensionFromMimeType(string? mimeType)
    {
        return mimeType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" or "image/x-ms-bmp" => ".bmp",
            "image/tiff" or "image/x-tiff" => ".tiff",
            "image/svg+xml" => ".svg",
            "video/mp4" or "video/x-mp4" => ".mp4",
            "video/x-matroska" or "video/mkv" => ".mkv",
            "video/webm" => ".webm",
            "video/quicktime" or "video/mov" => ".mov",
            "video/x-msvideo" or "video/avi" => ".avi",
            "video/x-ms-wmv" => ".wmv",
            "video/mpeg" => ".mpeg",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/ogg" or "audio/vorbis" => ".ogg",
            "audio/aac" or "audio/x-aac" => ".aac",
            "audio/flac" or "audio/x-flac" => ".flac",
            "audio/wav" or "audio/x-wav" or "audio/vnd.wave" => ".wav",
            "audio/opus" => ".opus",
            "audio/webm" => ".weba",
            "application/pdf" => ".pdf",
            "application/zip" => ".zip",
            "application/x-rar-compressed" or "application/vnd.rar" => ".rar",
            "application/x-7z-compressed" => ".7z",
            "application/gzip" or "application/x-gzip" => ".gz",
            "application/x-tar" => ".tar",
            "application/x-bzip2" => ".bz2",
            "text/plain" => ".txt",
            "text/html" => ".html",
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            _ => "",
        };
    }

    public static string GetExtensionForPhoto(Storage_FileType fileType)
    {
        return fileType switch
        {
            Storage_FileType.jpeg => ".jpg",
            Storage_FileType.png => ".png",
            Storage_FileType.webp => ".webp",
            Storage_FileType.gif => ".gif",
            Storage_FileType.mov => ".mov",
            Storage_FileType.mp4 => ".mp4",
            _ => ".jpg",
        };
    }

    private async Task<List<TLMessage>> FetchGroupedMessagesAsync(
        InputPeerChannel channel,
        long groupedId,
        int referenceMessageId
    )
    {
        if (_client == null)
        {
            return [];
        }

        try
        {
            var groupedMessages = new List<TLMessage>();

            // Ищем сообщения в диапазоне около текущего ID
            // Начинаем с более новых сообщений (меньший offset_id)
            for (var offset = 0; offset <= 300; offset += 100)
            {
                var messages = await _client.Messages_GetHistory(
                    peer: channel,
                    offset_id: referenceMessageId + offset,
                    add_offset: 0,
                    limit: 100
                );

                if (messages is null)
                {
                    break;
                }

                var foundInBatch = messages
                    .Messages.OfType<TLMessage>()
                    .Where(m => m.grouped_id == groupedId)
                    .ToList();

                groupedMessages.AddRange(foundInBatch);

                // Если нашли сообщения этой группы, можно остановиться
                if (foundInBatch.Count > 0)
                {
                    break;
                }
            }

            // Сортируем по ID и удаляем дубликаты
            groupedMessages = groupedMessages.OrderBy(m => m.ID).DistinctBy(m => m.ID).ToList();

            logger.LogInformation(
                "FetchGroupedMessagesAsync: найдено {Count} сообщений для grouped_id {GroupedId} (ref messageId: {RefId})",
                groupedMessages.Count,
                groupedId,
                referenceMessageId
            );

            return groupedMessages;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении сообщений группы (grouped_id: {GroupedId})",
                groupedId
            );
            return [];
        }
    }
}
