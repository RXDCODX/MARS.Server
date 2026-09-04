using MARS.Server.Services.BooruAutoPost.Entities;
using MARS.Server.Services.BooruShared.Entities;
using MARS.Server.Services.Telegram.WTelegram;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using TL;
using WTelegram;

namespace MARS.Server.Services.BooruAutoPost;

public class BooruTelegramPoster(
    ILogger<BooruTelegramPoster> logger,
    WTelegramClientService wTelegramClientService
) : IBooruTelegramPoster
{
    public async Task<OperationResult> PostAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        string message,
        TelegramParseMode parseMode,
        CancellationToken cancellationToken
    )
    {
        return await SendMediaAsync(
            chatId,
            fileBytes,
            fileName,
            message,
            parseMode,
            null,
            cancellationToken
        );
    }

    public async Task<OperationResult> SchedulePostAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        string message,
        TelegramParseMode parseMode,
        DateTime scheduleDate,
        CancellationToken cancellationToken
    )
    {
        return await SendMediaAsync(
            chatId,
            fileBytes,
            fileName,
            message,
            parseMode,
            scheduleDate,
            cancellationToken
        );
    }

    public async Task<
        OperationResult<List<TelegramScheduledMessageInfo>>
    > GetScheduledMessagesAsync(long chatId, CancellationToken cancellationToken)
    {
        var result = OperationResult<List<TelegramScheduledMessageInfo>>.Bad(
            "Не удалось получить отложенные сообщения Telegram"
        );

        try
        {
            var client = await wTelegramClientService.GetClientAsync(cancellationToken);
            if (client is not null)
            {
                var inputPeer = await ResolveInputPeerAsync(client, chatId);
                if (inputPeer is not null)
                {
                    var history = await client.Messages_GetScheduledHistory(inputPeer, 0);
                    var messages = history
                        .Messages.OfType<Message>()
                        .Select(m => new TelegramScheduledMessageInfo(
                            m.id,
                            DateTime.SpecifyKind(m.date, DateTimeKind.Utc)
                        ))
                        .ToList();

                    result = OperationResult<List<TelegramScheduledMessageInfo>>.Ok(
                        "Отложенные сообщения получены",
                        messages
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка получения отложенных сообщений Telegram канал {ChannelId}",
                chatId
            );
            result = OperationResult<List<TelegramScheduledMessageInfo>>.Bad(
                $"Ошибка Telegram: {ex.Message}"
            );
        }

        return result;
    }

    public async Task<OperationResult> DeleteScheduledMessagesAsync(
        long chatId,
        IReadOnlyCollection<int> messageIds,
        CancellationToken cancellationToken
    )
    {
        var result = OperationResult.Bad("Не удалось удалить отложенные сообщения Telegram");

        try
        {
            var client = await wTelegramClientService.GetClientAsync(cancellationToken);
            if (client is not null && messageIds.Count > 0)
            {
                var inputPeer = await ResolveInputPeerAsync(client, chatId);
                if (inputPeer is not null)
                {
                    await client.Messages_DeleteScheduledMessages(
                        inputPeer,
                        messageIds.ToArray()
                    );

                    result = OperationResult.Ok(
                        $"Удалено {messageIds.Count} отложенных сообщений Telegram"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка удаления отложенных сообщений Telegram канал {ChannelId}",
                chatId
            );
            result = OperationResult.Bad($"Ошибка Telegram: {ex.Message}");
        }

        return result;
    }

    private async Task<OperationResult> SendMediaAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        string message,
        TelegramParseMode parseMode,
        DateTime? scheduleDate,
        CancellationToken cancellationToken
    )
    {
        var result = OperationResult.Bad("Не удалось отправить в Telegram");

        try
        {
            var client = await wTelegramClientService.GetClientAsync(cancellationToken);
            if (client is not null)
            {
                var inputPeer = await ResolveInputPeerAsync(client, chatId);
                if (inputPeer is not null)
                {
                    var (readyBytes, converted) = await EnsureTelegramPhotoCompatibleAsync(
                        fileBytes,
                        fileName
                    );

                    if (converted)
                    {
                        logger.LogInformation(
                            "Изображение {FileName} сконвертировано для совместимости с Telegram",
                            fileName
                        );
                    }

                    var uploadBytes = readyBytes;
                    (var caption, MessageEntity[]? entities) = ConvertMessageToEntities(
                        message,
                        parseMode
                    );

                    try
                    {
                        await using var stream = new MemoryStream(uploadBytes);
                        var inputFile = await client.UploadFileAsync(stream, fileName);

                        await client.Messages_SendMedia(
                            peer: inputPeer,
                            media: new InputMediaUploadedPhoto { file = inputFile },
                            message: caption,
                            random_id: Random.Shared.NextInt64(),
                            entities: entities,
                            schedule_date: scheduleDate
                        );

                        result = OperationResult.Ok("Изображение отправлено в Telegram");
                    }
                    catch (Exception photoEx)
                        when (photoEx.Message.Contains("PHOTO_INVALID_DIMENSIONS")
                            || photoEx.Message.Contains("PHOTO_SAVE_FILE_INVALID")
                        )
                    {
                        if (!converted)
                        {
                            var (convertedBytes, _) = await EnsureTelegramPhotoCompatibleAsync(
                                uploadBytes,
                                fileName
                            );
                            uploadBytes = convertedBytes;
                        }

                        try
                        {
                            await using var retryStream = new MemoryStream(uploadBytes);
                            var retryFile = await client.UploadFileAsync(retryStream, fileName);

                            await client.Messages_SendMedia(
                                peer: inputPeer,
                                media: new InputMediaUploadedPhoto { file = retryFile },
                                message: caption,
                                random_id: Random.Shared.NextInt64(),
                                entities: entities,
                                schedule_date: scheduleDate
                            );

                            result = OperationResult.Ok("Изображение отправлено в Telegram");
                        }
                        catch
                        {
                            await using var docStream = new MemoryStream(fileBytes);
                            var docFile = await client.UploadFileAsync(docStream, fileName);

                            await client.Messages_SendMedia(
                                peer: inputPeer,
                                media: new InputMediaUploadedDocument
                                {
                                    file = docFile,
                                    mime_type = "application/octet-stream",
                                    attributes =
                                    [
                                        new DocumentAttributeFilename { file_name = fileName },
                                    ],
                                },
                                message: caption,
                                random_id: Random.Shared.NextInt64(),
                                entities: entities,
                                schedule_date: scheduleDate
                            );

                            result = OperationResult.Ok(
                                "Изображение отправлено в Telegram как документ"
                            );
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка отправки в Telegram канал {ChannelId}", chatId);
            result = OperationResult.Bad($"Ошибка Telegram: {ex.Message}");
        }

        return result;
    }

    private static (string caption, MessageEntity[]? entities) ConvertMessageToEntities(
        string message,
        TelegramParseMode parseMode
    )
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return ("", null);
        }

        MessageEntity[]? entities = null;
        var caption = message;

        try
        {
            switch (parseMode)
            {
                case TelegramParseMode.Html:
                    var htmlText = message;
                    entities = HtmlText.HtmlToEntities(null, ref htmlText);
                    caption = htmlText;
                    break;
                case TelegramParseMode.Markdown:
                    var mdText = message;
                    entities = Markdown.MarkdownToEntities(null, ref mdText);
                    caption = mdText;
                    break;
            }
        }
        catch (Exception)
        {
            entities = null;
        }

        return (caption, entities is { Length: > 0 } ? entities : null);
    }

    private static async Task<(byte[] Bytes, bool Converted)> EnsureTelegramPhotoCompatibleAsync(
        byte[] fileBytes,
        string fileName
    )
    {
        var converted = false;
        var resultBytes = fileBytes;

        try
        {
            using var image = await Image.LoadAsync(new MemoryStream(fileBytes));

            var needsResize =
                image.Width + image.Height > 10000
                || (double)image.Width / image.Height > 20
                || (double)image.Height / image.Width > 20;
            var needsCompression = fileBytes.Length > 10 * 1024 * 1024;

            if (needsResize || needsCompression)
            {
                var maxDimension = needsResize ? 5000 : 2560;
                image.Mutate(x =>
                    x.Resize(
                        new ResizeOptions
                        {
                            Size = new Size(maxDimension, maxDimension),
                            Mode = ResizeMode.Max,
                        }
                    )
                );

                var outputStream = new MemoryStream();
                await image.SaveAsync(
                    outputStream,
                    new JpegEncoder { Quality = 85 },
                    CancellationToken.None
                );
                resultBytes = outputStream.ToArray();
                converted = true;
            }
        }
        catch (UnknownImageFormatException)
        {
            // Не является изображением — отправляем как есть
        }

        return (resultBytes, converted);
    }

    private static async Task<InputPeerChannel?> ResolveInputPeerAsync(Client client, long chatId)
    {
        var actualChannelId = -chatId - 1000000000000;

        var allChats = await client.Messages_GetAllChats();
        var channel = allChats
            .chats.Values.OfType<Channel>()
            .FirstOrDefault(c => c.id == actualChannelId);

        return channel is not null ? new InputPeerChannel(channel.id, channel.access_hash) : null;
    }
}
