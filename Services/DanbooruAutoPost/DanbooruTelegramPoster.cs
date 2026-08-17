using MARS.Server.Services.Telegram.WTelegram;
using Microsoft.Extensions.Logging;
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
        return await SendMediaAsync(
            chatId,
            fileBytes,
            fileName,
            scheduleDate,
            cancellationToken
        );
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
            if (client is null)
            {
                return OperationResult.Bad("WTelegram клиент недоступен");
            }

            var inputPeer = await ResolveInputPeerAsync(client, chatId);
            if (inputPeer is null)
            {
                return OperationResult.Bad(
                    $"Не удалось найти Telegram канал {chatId}"
                );
            }

            await using var stream = new MemoryStream(fileBytes);
            var inputFile = await client.UploadFileAsync(stream, fileName);

            try
            {
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
                when (photoEx.Message.Contains("PHOTO_INVALID_DIMENSIONS"))
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка отправки в Telegram канал {ChannelId}", chatId);
            result = OperationResult.Bad($"Ошибка Telegram: {ex.Message}");
        }

        return result;
    }

    private static async Task<InputPeerChannel?> ResolveInputPeerAsync(
        Client client,
        long chatId
    )
    {
        // chatId формат: -100XXXXXXXXXX (Bot API формат)
        // Нужно конвертировать в TL channel ID: -chatId - 1000000000000
        var actualChannelId = -chatId - 1000000000000;

        var allChats = await client.Messages_GetAllChats();
        var channel = allChats
            .chats.Values.OfType<Channel>()
            .FirstOrDefault(c => c.id == actualChannelId);

        return channel is not null
            ? new InputPeerChannel(channel.id, channel.access_hash)
            : null;
    }
}
