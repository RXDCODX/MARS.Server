using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.Telegram.WTelegram;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using TL;
using WTelegram;

namespace MARS.Server.Services.DanbooruAutoPost;

public class DanbooruTelegramPoster(
    ILogger<DanbooruTelegramPoster> logger,
    WTelegramClientService wTelegramClientService
) : IDanbooruTelegramPoster
{
    public async Task<OperationResult> PostAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        return await SendMediaAsync(chatId, fileBytes, fileName, null, cancellationToken);
    }

    public async Task<OperationResult> SchedulePostAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        DateTime scheduleDate,
        CancellationToken cancellationToken
    )
    {
        return await SendMediaAsync(chatId, fileBytes, fileName, scheduleDate, cancellationToken);
    }

    private async Task<OperationResult> SendMediaAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
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

                    try
                    {
                        await using var stream = new MemoryStream(uploadBytes);
                        var inputFile = await client.UploadFileAsync(stream, fileName);

                        await client.Messages_SendMedia(
                            peer: inputPeer,
                            media: new InputMediaUploadedPhoto { file = inputFile },
                            message: "",
                            random_id: Random.Shared.NextInt64(),
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
                                message: "",
                                random_id: Random.Shared.NextInt64(),
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
                                message: "",
                                random_id: Random.Shared.NextInt64(),
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
        // chatId формат: -100XXXXXXXXXX (Bot API формат)
        // Нужно конвертировать в TL channel ID: -chatId - 1000000000000
        var actualChannelId = -chatId - 1000000000000;

        var allChats = await client.Messages_GetAllChats();
        var channel = allChats
            .chats.Values.OfType<Channel>()
            .FirstOrDefault(c => c.id == actualChannelId);

        return channel is not null ? new InputPeerChannel(channel.id, channel.access_hash) : null;
    }
}
