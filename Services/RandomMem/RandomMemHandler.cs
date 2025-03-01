using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem.Entity;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace MARS.Server.Services.RandomMem;

public class RandomMemHandler(
    IWebHostEnvironment environment,
    PyroAlertsHelper helper,
    ILogger<RandomMemHandler> logger,
    IDbContextFactory<AppDbContext> contextFactory,
    IHostApplicationLifetime applicationLifetime
) : ITelegramusService
{
    public readonly string AlertsPath = Path.Combine(environment.WebRootPath, "Alerts");

    private string? LastMediaGroupId { get; set; }
    private bool IsGoldMediaGroup { get; set; }
    private CancellationToken CancellationToken { get; set; } =
        applicationLifetime.ApplicationStopping;

    public async Task HandMessage(ITelegramBotClient client, Update update)
    {
        if (update.Type == UpdateType.Message)
        {
            Message message = update.Message!;
            if (message.Chat.Id == 833194345)
            {
                if (message.MediaGroupId == null)
                {
                    await Process(client, message);
                    LastMediaGroupId = null;
                    IsGoldMediaGroup = false;
                }
                else
                {
                    if (LastMediaGroupId == null || LastMediaGroupId != message.MediaGroupId)
                    {
                        LastMediaGroupId = message.MediaGroupId;
                        var isGold = !string.IsNullOrWhiteSpace(message.Caption);
                        IsGoldMediaGroup = isGold;
                    }

                    if (LastMediaGroupId == message.MediaGroupId)
                    {
                        await Process(client, message, IsGoldMediaGroup);
                    }
                }
            }
        }
    }

    private async Task Process(ITelegramBotClient client, Message message, bool isGold = false)
    {
        var fileInfo = await helper.GetFilePath(client, message);

        if (fileInfo == null)
        {
            return;
        }

        if (!isGold)
        {
            isGold = !string.IsNullOrWhiteSpace(message.Caption);
        }

        var folderPath = isGold ? Path.Combine(AlertsPath, "Gold") : AlertsPath;
        var downloadPath = folderPath + "\\" + fileInfo.FilePath;

        MediaType type = await Path.GetExtension(fileInfo.FilePath).GetFileMediaTypeAsync();
        string caption;
        const string answer1 = "Скачал твой файл ({1})";
        const string answer = "такой файл уже есть, обновил время последнего акцесса до {0}";
        const string answer2 = "С мемом чето не так, ппц брат.";
        const string goldAnswer1 = "Скачал твой ЗОЛОТОЙ файл ({1}) и вставил его в качестве мема!";
        const string goldAnswer =
            "такой ЗОЛОТОЙ файл уже есть, обновил время последнего акцесса до {0}";
        const string goldAnswer2 = "С ЗОЛОТЫМ мемом чето не так, ппц брат.";
        switch (type)
        {
            case MediaType.Video:
                if (!File.Exists(downloadPath))
                {
                    await helper.DownloadFile(client, fileInfo, folderPath);
                    caption = isGold ? goldAnswer1 : answer1;
                }
                else
                {
                    File.SetLastAccessTime(downloadPath, DateTimeOffset.Now.LocalDateTime);
                    caption = isGold ? goldAnswer : answer;
                }

                break;
            case MediaType.Image:
                if (!File.Exists(downloadPath))
                {
                    await helper.DownloadFile(client, fileInfo, folderPath);
                    caption = isGold ? goldAnswer1 : answer1;
                }
                else
                {
                    File.SetLastAccessTime(downloadPath, DateTimeOffset.Now.LocalDateTime);
                    caption = isGold ? goldAnswer : answer;
                }

                break;
            default:
                caption = isGold ? goldAnswer2 : answer2;
                break;
        }

        try
        {
            await CreateMemeOrder(downloadPath);

            await client.SendMessage(
                833194345,
                string.Format(caption, DateTimeOffset.Now.LocalDateTime, fileInfo.FilePath),
                cancellationToken: CancellationToken
            );
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }
    }

    private async Task CreateMemeOrder(string filePath)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(CancellationToken);

        var order = await dbContext.RandomMemeOrder.MaxAsync(e => e.Order, CancellationToken);

        var types = dbContext.RandomMemeType.AsAsyncEnumerable();

        var newOrder = new MemeOrder() { FilePath = filePath, Order = ++order };

        await foreach (var type in types)
        {
            if (filePath.Contains(type.FolderPath, StringComparison.OrdinalIgnoreCase))
            {
                newOrder.MemeTypeId = type.Id;
            }
        }

        if (newOrder.MemeTypeId is 0 or null)
        {
            newOrder.MemeTypeId = 1;
        }

        await dbContext.RandomMemeOrder.AddAsync(newOrder, CancellationToken);

        await dbContext.SaveChangesAsync(CancellationToken);
    }
}
