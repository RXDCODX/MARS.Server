using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.Telegram.GooglePhotos;

public class TelegramGooglePhotosService(
    ILogger<TelegramGooglePhotosService> logger,
    GooglePhotosAuthService authService,
    GooglePhotosApiClient apiClient,
    IHostApplicationLifetime applicationLifetime,
    IOptions<GooglePhotosConfiguration> googlePhotosOptions
) : ITelegramusService
{
    private readonly GooglePhotosConfiguration _config = googlePhotosOptions.Value;
    private CancellationToken StoppingToken => applicationLifetime.ApplicationStopping;

    public async Task HandMessage(ITelegramBotClient client, Update update)
    {
        if (update is { Type: UpdateType.Message, Message: { } message })
        {
            if (message.Chat.Id == _config.TelegramChatId)
            {
                await HandleMessageAsync(client, message);
            }
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient client, Message message)
    {
        var isAuthorized = await authService.IsAuthorizedAsync(StoppingToken);
        if (!isAuthorized)
        {
            await client.SendMessage(
                message.Chat.Id,
                "❌ Google Photos не авторизован",
                cancellationToken: StoppingToken
            );
            return;
        }

        if (message.Photo is { Length: > 0 })
        {
            await ProcessPhotosAsync(client, message);
        }
        else if (message.Document?.MimeType?.StartsWith("image/") == true)
        {
            await ProcessDocumentAsync(client, message.Document);
        }
    }

    private async Task ProcessPhotosAsync(ITelegramBotClient client, Message message)
    {
        try
        {
            if (message.Photo == null || message.Photo.Length == 0)
            {
                await client.SendMessage(
                    message.Chat.Id,
                    "❌ Нет фото",
                    cancellationToken: StoppingToken
                );
                return;
            }
            var largestPhoto = message.Photo.OrderByDescending(p => p.FileSize ?? 0).First();
            var fileInfo = await client.GetFile(largestPhoto.FileId, StoppingToken);

            if (fileInfo?.FilePath == null)
            {
                await client.SendMessage(
                    message.Chat.Id,
                    "❌ Ошибка файла",
                    cancellationToken: StoppingToken
                );
                return;
            }

            await using var photoStream = new MemoryStream();
            await client.DownloadFile(fileInfo.FilePath, photoStream, StoppingToken);
            photoStream.Position = 0;

            var fileName = $"telegram_{message.MessageId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var uploadResult = await apiClient.UploadPhotoAsync(
                photoStream,
                fileName,
                StoppingToken
            );

            if (uploadResult.Success)
            {
                logger.LogInformation("Фото загружено: {MediaItemId}", uploadResult.Data);
                await client.SendMessage(
                    message.Chat.Id,
                    $"✅ {uploadResult.Message}",
                    cancellationToken: StoppingToken
                );
            }
            else
            {
                logger.LogError("Ошибка загрузки: {Error}", uploadResult.Message);
                await client.SendMessage(
                    message.Chat.Id,
                    $"❌ {uploadResult.Message}",
                    cancellationToken: StoppingToken
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обработки фото");
            await client.SendMessage(
                message.Chat.Id,
                $"❌ Ошибка: {ex.Message}",
                cancellationToken: StoppingToken
            );
        }
    }

    private async Task ProcessDocumentAsync(ITelegramBotClient client, Document document)
    {
        try
        {
            var fileInfo = await client.GetFile(document.FileId, StoppingToken);

            if (fileInfo?.FilePath == null)
            {
                return;
            }

            await using var documentStream = new MemoryStream();
            await client.DownloadFile(fileInfo.FilePath, documentStream, StoppingToken);
            documentStream.Position = 0;

            var fileName = document.FileName ?? $"document_{DateTime.Now:yyyyMMdd_HHmmss}";
            var uploadResult = await apiClient.UploadPhotoAsync(
                documentStream,
                fileName,
                StoppingToken
            );

            if (uploadResult.Success)
            {
                logger.LogInformation("Документ загружен: {MediaItemId}", uploadResult.Data);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обработки документа");
        }
    }
}
