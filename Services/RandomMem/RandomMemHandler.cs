using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem.Entity;
using MARS.Server.Services.ServiceManager;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace MARS.Server.Services.RandomMem;

public class RandomMemHandler(
    IWebHostEnvironment environment,
    PyroAlertsHelper helper,
    ILogger<RandomMemHandler> logger,
    IDbContextFactory<AppDbContext> contextFactory,
    IHostApplicationLifetime applicationLifetime
) : ManagedServiceBase(logger), ITelegramusService
{
    public override string ServiceName => "randommemhandler";
    public override string DisplayName => "Random Meme Handler";
    public override string Description => "Обработчик мемов Telegram";
    public override bool IsServiceActive { get; set; } = true;

    public readonly string[] AlertsPaths = environment.IsProduction()
        ? [Path.Combine(environment.WebRootPath, "Alerts", "random_meme"), GetDevAlerts()]
        : [Path.Combine(environment.WebRootPath, "Alerts", "random_meme")];
    private string? LastMediaGroupId { get; set; }
    private bool IsGoldMediaGroup { get; set; }
    private CancellationToken CancellationToken { get; set; } =
        applicationLifetime.ApplicationStopping;
    private List<long> BlockedUsers { get; set; } = [];
    private List<long> AllowdUsers { get; set; } = [];

    public Task HandMessage(ITelegramBotClient client, Update update)
    {
        if (update.Type == UpdateType.Message && IsServiceActive)
        {
            var message = update.Message!;
            var chatId = message.Chat.Id;

            if (!BlockedUsers.Contains(chatId))
            {
                return Task.Factory.StartNew(
                    async () =>
                    {
                        bool pass;

                        if (AllowdUsers.Contains(chatId))
                        {
                            pass = true;
                        }
                        else
                        {
                            await using var dbContext = await contextFactory.CreateDbContextAsync(
                                CancellationToken
                            );
                            pass = await dbContext.TelegramUsers.AnyAsync(
                                e => e.UserId == chatId && e.IsRandomMemeSendler,
                                cancellationToken: CancellationToken
                            );
                            AllowdUsers.Add(chatId);
                        }

                        if (pass)
                        {
                            if (message.MediaGroupId == null)
                            {
                                await Process(client, message);
                                LastMediaGroupId = null;
                                IsGoldMediaGroup = false;
                            }
                            else
                            {
                                if (
                                    LastMediaGroupId == null
                                    || LastMediaGroupId != message.MediaGroupId
                                )
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
                        else
                        {
                            if (!BlockedUsers.Contains(chatId))
                            {
                                BlockedUsers.Add(chatId);
                            }
                        }
                    },
                    CancellationToken
                );
            }
        }
        return Task.CompletedTask;
    }

    private async Task Process(ITelegramBotClient client, Message message, bool isGold = false)
    {
        var fileInfo = await helper.GetTgFileInfo(client, message);

        if (fileInfo == null)
        {
            return;
        }

        if (!isGold)
        {
            isGold = !string.IsNullOrWhiteSpace(message.Caption);
        }

        string caption = null!;
        const string answer1 = "Скачал твой файл ({1})";
        const string answer = "такой файл уже есть ({1}), обновил время последнего акцесса до {0}";
        const string answer2 = "С мемом чето не так, ппц брат.";
        const string goldAnswer1 = "Скачал твой ЗОЛОТОЙ файл ({1}) и вставил его в качестве мема!";
        const string goldAnswer =
            "такой ЗОЛОТОЙ файл уже есть, обновил время последнего акцесса до {0}";
        const string goldAnswer2 = "С ЗОЛОТЫМ мемом чето не так, ппц брат.";

        for (var index = 0 ; index < AlertsPaths.Length ; index++)
        {
            var alertsPath = AlertsPaths[index];
            var folderPath = isGold ? Path.Combine(alertsPath, "Gold") : alertsPath;
            var downloadPath = folderPath + "\\" + fileInfo.FilePath;

            MediaType type = await Path.GetExtension(fileInfo.FilePath).GetFileMediaTypeAsync();

            switch (type)
            {
                case MediaType.Video:
                    if (!File.Exists(downloadPath))
                    {
                        await helper.DownloadFileAndCache(client, fileInfo, folderPath);
                        if (index == 0)
                        {
                            caption = isGold ? goldAnswer1 : answer1;
                        }
                    }
                    else
                    {
                        File.SetLastAccessTime(downloadPath, DateTimeOffset.Now.LocalDateTime);
                        if (index == 0)
                        {
                            caption = isGold ? goldAnswer : answer;
                        }
                    }

                    break;
                case MediaType.Image:
                    if (!File.Exists(downloadPath))
                    {
                        await helper.DownloadFileAndCache(client, fileInfo, folderPath);
                        if (index == 0)
                        {
                            caption = isGold ? goldAnswer1 : answer1;
                        }
                    }
                    else
                    {
                        File.SetLastAccessTime(downloadPath, DateTimeOffset.Now.LocalDateTime);
                        if (index == 0)
                        {
                            caption = isGold ? goldAnswer : answer;
                        }
                    }

                    break;
                default:
                    if (index == 0)
                    {
                        caption = isGold ? goldAnswer2 : answer2;
                    }

                    break;
            }

            if (index == 0)
            {
                await CreateMemeOrder(downloadPath);
            }
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(caption))
            {
                await client.SendMessage(
                    message.Chat.Id,
                    string.Format(caption, DateTimeOffset.Now.LocalDateTime, fileInfo.FilePath),
                    cancellationToken: CancellationToken
                );
            }
        }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
    }

    private async Task CreateMemeOrder(string filePath)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(CancellationToken);

        var types = dbContext.RandomMemeType.AsAsyncEnumerable();

        var typeId = 0;

        await foreach (var type in types)
        {
            // Собираем полный путь: basePath + type.FolderPath
            var fullFolderPath = Path.Combine(environment.WebRootPath, type.FolderPath);

            // Нормализуем пути
            var normalizedFilePath = Path.GetFullPath(filePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var normalizedFolderPath = Path.GetFullPath(fullFolderPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Проверяем, содержится ли folderPath в filePath
            if (
                normalizedFilePath.StartsWith(
                    normalizedFolderPath,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                typeId = type.Id;
                break;
            }
        }

        if (typeId is 0)
        {
            typeId = 1;
        }

        var order = await dbContext
            .RandomMemeOrder.Where(e => e.MemeTypeId == typeId)
            .MaxAsync(e => e.Order, CancellationToken);

        var newOrder = new MemeOrder()
        {
            FilePath = filePath,
            Order = ++order,
            MemeTypeId = typeId,
        };

        await dbContext.RandomMemeOrder.AddAsync(newOrder, CancellationToken);

        await dbContext.SaveChangesAsync(CancellationToken);
    }

    private static string GetDevAlerts()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot =
            FindProjectRoot(currentDir)
            ?? throw new DirectoryNotFoundException(
                "Не удалось найти корень проекта (папку с .csproj)"
            );
        return Path.Combine(projectRoot, "wwwroot", "Alerts", "random_meme");
    }

    // Ищет корень проекта (папку с .csproj), игнорируя вложенные /bin
    private static string? FindProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);

        while (dir != null)
        {
            // Проверяем, есть ли в этой папке .csproj файл (признак корня проекта)
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                return dir.FullName;
            }

            // Если дошли до корня диска и не нашли .csproj — выходим
            if (dir.Parent == null)
            {
                break;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
