using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service;
using System.Text;

namespace MARS.Server.Services.Twitch.Media;

public class TwitchMediaTranscodeWorker(
    IServiceScopeFactory serviceScopeFactory,
    ITwitchMediaPreparationService twitchMediaPreparationService,
    ITelegramBotClient telegramBotClient,
    ILogger<TwitchMediaTranscodeWorker> logger
) : BackgroundService
{
    private const int TelegramMessageMaxLength = 3900;

    private sealed class ProcessedMediaEntry
    {
        public string SourcePath { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
    }

    public bool IsServiceActive { get; set; } = true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TranscodePendingMediaAsync(stoppingToken);

        using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        while (IsServiceActive && await periodicTimer.WaitForNextTickAsync(stoppingToken))
        {
            await TranscodePendingMediaAsync(stoppingToken);
        }
    }

    private async Task TranscodePendingMediaAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var randomMemeService = scope.ServiceProvider.GetRequiredService<IRandomMemeService>();
            var mediaOrders = await randomMemeService.GetAllMemeOrdersAsync(cancellationToken);
            var processedMediaEntries = new List<ProcessedMediaEntry>();

            foreach (var mediaOrder in mediaOrders)
            {
                await SendTelegramNotificationAsync(
                    $"Начата обработка файла: {mediaOrder.FilePath}",
                    cancellationToken
                );

                var isSuccess = false;

                try
                {
                    var media = await twitchMediaPreparationService.PrepareMediaAsync(
                        mediaOrder,
                        null,
                        cancellationToken
                    );
                    isSuccess = media is not null;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Ошибка обработки файла {FilePath}", mediaOrder.FilePath);
                }

                processedMediaEntries.Add(
                    new ProcessedMediaEntry { SourcePath = mediaOrder.FilePath, IsSuccess = isSuccess }
                );
            }

            if (processedMediaEntries.Count > 0)
            {
                await SendTelegramNotificationAsync(
                    BuildBatchSummary(processedMediaEntries),
                    cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось выполнить фоновую перекодировку медиа");
        }
    }

    private string BuildBatchSummary(IReadOnlyList<ProcessedMediaEntry> processedMediaEntries)
    {
        var result = new StringBuilder();
        var totalCount = processedMediaEntries.Count;
        var successCount = processedMediaEntries.Count(x => x.IsSuccess);
        var failedCount = totalCount - successCount;

        result.AppendLine("Обработка файлов завершена");
        result.AppendLine($"Всего файлов: {totalCount}");
        result.AppendLine($"Успешно: {successCount}");
        result.AppendLine($"С ошибкой: {failedCount}");
        result.AppendLine("Полный список:");

        for (var index = 0; index < processedMediaEntries.Count; index++)
        {
            var entry = processedMediaEntries[index];
            var statusText = entry.IsSuccess ? "успех" : "ошибка";
            result.AppendLine($"{index + 1}. {entry.SourcePath} [{statusText}]");
        }

        return result.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> SplitTelegramMessage(string message)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(message))
        {
            result.Add(string.Empty);
        }
        else
        {
            var lines = message.Split(Environment.NewLine);
            var chunkBuilder = new StringBuilder();

            foreach (var line in lines)
            {
                var candidateLine = chunkBuilder.Length == 0 ? line : Environment.NewLine + line;

                if (chunkBuilder.Length + candidateLine.Length > TelegramMessageMaxLength)
                {
                    if (chunkBuilder.Length > 0)
                    {
                        result.Add(chunkBuilder.ToString());
                        chunkBuilder.Clear();
                    }

                    if (line.Length > TelegramMessageMaxLength)
                    {
                        var startIndex = 0;

                        while (startIndex < line.Length)
                        {
                            var length = Math.Min(TelegramMessageMaxLength, line.Length - startIndex);
                            result.Add(line.Substring(startIndex, length));
                            startIndex += length;
                        }
                    }
                    else
                    {
                        chunkBuilder.Append(line);
                    }
                }
                else
                {
                    chunkBuilder.Append(candidateLine);
                }
            }

            if (chunkBuilder.Length > 0)
            {
                result.Add(chunkBuilder.ToString());
            }
        }

        return result;
    }

    private async Task SendTelegramNotificationAsync(
        string message,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var messageParts = SplitTelegramMessage(message);

            foreach (var messagePart in messageParts)
            {
                await telegramBotClient.SendMessage(
                    TelegramExstension.Rxdcodx,
                    messagePart,
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось отправить Telegram-уведомление о конвертации");
        }
    }
}