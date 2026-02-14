using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchRandomBadAppleDay;

public class RandomBadAppleDay(
    ChannelRewardsService channelRewardsService,
    ILogger<RandomBadAppleDay> logger,
    IWebHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ITwitchClient twitchClient,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext
) : TemporaryReward(channelRewardsService, (ILogger<TemporaryReward>)(object)logger, environment)
{
    private readonly Random _random = new();
    private DateTimeOffset _lastActivation = DateTimeOffset.MinValue;
    private const int CooldownMinutes = 90; // 1.5 часа
    private const double ActivationChancePerWord = 0.002; // 0.2% на слово
    private const string BadApplesFolder = "badapples";
    private List<string> _badAppleVideos = [];

    public override string AlertDisplayName { get; set; } = "Bad Apple";
    public override string AlertDescription { get; set; } =
        "Твоя уникальная возможность активации BadApple";
    public override Color Color { get; set; } = Color.Black;
    public override int Cost { get; init; } = 45;
    public override Func<DateTime, bool> IsRewardEnabled { get; set; } = time => false;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            // Загружаем доступные видео
            LoadAvailableVideos();

            // Если видео есть, регистрируем обработчик сообщений
            if (_badAppleVideos.Count > 0)
            {
                twitchClient.OnMessageReceived += OnMessageReceived;
                logger.LogInformation(
                    "RandomBadAppleDay запущен. Найдено {Count} видео Bad Apple. Награда может активироваться с вероятностью {Chance}% на каждое слово.",
                    _badAppleVideos.Count,
                    ActivationChancePerWord * 100
                );
            }
            else
            {
                logger.LogWarning(
                    "RandomBadAppleDay: не найдены видео файлы в папке wwwroot/{BadApplesFolder}. Награда не будет активирована.",
                    BadApplesFolder
                );
            }
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            twitchClient.OnMessageReceived -= OnMessageReceived;
        });

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancelToken)
    {
        twitchClient.OnMessageReceived -= OnMessageReceived;
        await base.StopAsync(cancelToken);
    }

    private void LoadAvailableVideos()
    {
        _badAppleVideos.Clear();

        try
        {
            var badApplesPath = Path.Combine(environment.WebRootPath, BadApplesFolder);

            if (!Directory.Exists(badApplesPath))
            {
                logger.LogWarning("Папка {Path} не существует", badApplesPath);
                return;
            }

            var videoExtensions = new[] { ".mp4", ".webm" };
            var videoFiles = Directory
                .GetFiles(badApplesPath)
                .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            _badAppleVideos = videoFiles
                .Select(f => Path.Combine(BadApplesFolder, Path.GetFileName(f)))
                .ToList();

            logger.LogInformation(
                "Загружено {Count} видео Bad Apple из папки {Path}",
                _badAppleVideos.Count,
                badApplesPath
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при загрузке видео из папки {BadApplesFolder}",
                BadApplesFolder
            );
        }
    }

    private async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        // Проверяем, что это сообщение из нужного канала
        if (
            !e.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        // Пропускаем сообщения из черного списка
        if (
            TwitchExstension.BlackList.Any(u =>
                u.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        // Пропускаем если нет видео файлов
        if (_badAppleVideos.Count == 0)
        {
            return;
        }

        // Проверяем кулдаун
        if (DateTimeOffset.UtcNow - _lastActivation < TimeSpan.FromMinutes(CooldownMinutes))
        {
            return;
        }

        // Разбираем сообщение на слова
        var words = e.ChatMessage.Message.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries
        );

        // Для каждого слова проверяем вероятность активации
        foreach (var word in words)
        {
            if (_random.NextDouble() < ActivationChancePerWord)
            {
                await ActivateRewardAsync(e.ChatMessage.Username, e.ChatMessage.DisplayName);
                return; // Активируем только один раз за сообщение
            }
        }
    }

    private async Task ActivateRewardAsync(string username, string displayName)
    {
        try
        {
            _lastActivation = DateTimeOffset.UtcNow;

            // Выбираем случайное видео
            var randomVideo = _badAppleVideos[_random.Next(_badAppleVideos.Count)];
            var videoPath = $"/{randomVideo}"; // badapples/video.mp4

            // Отправляем сообщение в чат
            var duration = TimeSpan.FromMinutes(10);
            var endTime = DateTimeOffset.UtcNow.Add(duration);
            var timeString = endTime.ToString("HH:mm");

            var chatMessage =
                $"💀 {displayName} случайным образом активировал(а) {AlertDisplayName}! Награда будет доступна до {timeString}! 💀";

            await twitchClient.SendMessageToMainTwitchAsync(chatMessage, logger);

            // Отправляем Alert через SignalR
            await SendAlertAsync(videoPath);

            logger.LogInformation(
                "RandomBadAppleDay активирована пользователем {Username} ({DisplayName}). Видео: {Video}",
                username,
                displayName,
                randomVideo
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при активации RandomBadAppleDay");
        }
    }

    private async Task SendAlertAsync(string videoPath)
    {
        try
        {
            var fileName = Path.GetFileName(videoPath);
            var extension = Path.GetExtension(fileName).TrimStart('.');

            // Создаем MediaInfo для видео
            var mediaInfo = new MediaInfo
            {
                TextInfo = new MediaTextInfo { Text = "Bad Apple!!", TextColor = "#FFFFFF" },
                FileInfo = new MediaFileInfo
                {
                    Type = MediaType.Video,
                    FilePath = videoPath,
                    IsLocalFile = true,
                    FileName = fileName,
                    Extension = extension,
                },
                PositionInfo = new MediaPositionInfo
                {
                    RandomCoordinates = true,
                    Height = 400,
                    Width = 400,
                    IsProportion = true,
                },
                MetaInfo = new MediaMetaInfo
                {
                    DisplayName = AlertDisplayName,
                    Duration = 600, // 10 минут в секундах
                    Volume = 100,
                    Priority = MediaAlertPriority.High,
                },
                StylesInfo = new MediaStylesInfo(),
            };

            var mediaDto = new MediaDto(mediaInfo);

            // Отправляем Alert всем клиентам через SignalR
            await hubContext.Clients.All.Alert(mediaDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке Alert в SignalR");
        }
    }
}
