using MARS.Server.Services.PyroAlerts;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace MARS.Server.Services.RandomMem;

public class RandomMemHandler(
    IWebHostEnvironment environment,
    PyroAlertsHelper helper,
    ILogger<RandomMemHandler> logger
) : ITelegramusService
{
    public readonly string AlertsPath = Path.Combine(
        environment.WebRootPath,
        "Alerts",
        "random_meme"
    );

    private string? LastMediaGroupId { get; set; }
    private bool IsGoldMediaGroup { get; set; }

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
        var caption = string.Empty;
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
            await client.SendMessage(
                833194345,
                string.Format(caption, DateTimeOffset.Now.LocalDateTime, fileInfo.FilePath)
            );
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }
    }
}
