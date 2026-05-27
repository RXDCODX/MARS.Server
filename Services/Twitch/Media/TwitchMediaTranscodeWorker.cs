using System.Text;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service;

namespace MARS.Server.Services.Twitch.Media;

public class TwitchMediaTranscodeWorker(
    IServiceScopeFactory serviceScopeFactory,
    ITwitchMediaPreparationService twitchMediaPreparationService,
    ITelegramBotClient telegramBotClient,
    ILogger<TwitchMediaTranscodeWorker> logger
) : BackgroundService
{
    private const int TelegramMessageMaxLength = 3900;
    private readonly System.Threading.SemaphoreSlim _runLock = new(1, 1);

    public bool IsServiceActive { get; set; } = true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Try to run initial pass if available
        if (await _runLock.WaitAsync(0, stoppingToken))
        {
            try
            {
                await TranscodePendingMediaAsync(stoppingToken);
            }
            finally
            {
                _runLock.Release();
            }
        }

        using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        while (IsServiceActive && await periodicTimer.WaitForNextTickAsync(stoppingToken))
        {
            // If previous run still executing, skip this tick to keep single-threaded behavior
            if (!await _runLock.WaitAsync(0, stoppingToken))
            {
                continue;
            }

            try
            {
                await TranscodePendingMediaAsync(stoppingToken);
            }
            finally
            {
                _runLock.Release();
            }
        }
    }

    private async Task TranscodePendingMediaAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var randomMemeService = scope.ServiceProvider.GetRequiredService<IRandomMemeService>();
            var mediaOrders = (await randomMemeService.GetAllMemeOrdersAsync(cancellationToken)).ToList();
            var transcodeReports = new List<string>();

            foreach (var mediaOrder in mediaOrders)
            {
                try
                {
                    var media = await twitchMediaPreparationService.PrepareMediaAsync(
                        mediaOrder,
                        null,
                        cancellationToken,
                        report =>
                        {
                            transcodeReports.Add(report);
                            return Task.CompletedTask;
                        }
                    );

                    _ = media;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Ошибка обработки файла {FilePath}", mediaOrder.FilePath);
                }
            }

            if (transcodeReports.Count > 0)
            {
                await SendTelegramNotificationAsync(
                    BuildBatchSummary(mediaOrders.Count, transcodeReports),
                    cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось выполнить фоновую перекодировку медиа");
        }
    }

    private string BuildBatchSummary(int totalCount, IReadOnlyList<string> transcodeReports)
    {
        var result = new StringBuilder();
        var convertedCount = transcodeReports.Count;

        result.AppendLine("Обработка файлов завершена");
        result.AppendLine($"Всего файлов: {totalCount}");
        result.AppendLine($"Требовали конвертацию: {convertedCount}");
        result.AppendLine("Полный список:");

        for (var index = 0 ; index < transcodeReports.Count ; index++)
        {
            result.AppendLine($"{index + 1}. {transcodeReports[index]}");
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