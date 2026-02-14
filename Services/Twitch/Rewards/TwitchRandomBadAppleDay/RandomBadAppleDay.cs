using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchRandomBadAppleDay;

public class RandomBadAppleDay(
    ChannelRewardsService channelRewardsService,
    EventSubWebsocketClient wsClient,
    ILogger<RandomBadAppleDay> logger,
    IWebHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ITwitchClient twitchClient,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext
) : TemporaryReward(channelRewardsService, logger, environment)
{
    private readonly Random _random = new();
    private DateTime _lastActivation = DateTime.MinValue;
    private const int CooldownMinutes = 90; // 1.5 часа
    private const double ActivationChancePerWord = 0.002; // 0.2% на слово
    private const string BadApplesFolder = "badapples";

    public override string AlertDisplayName { get; set; } = "Bad Apple";
    public override string AlertDescription { get; set; } =
        "Твоя уникальная возможность активации BadApple";
    public override Color Color { get; set; } = Color.Black;
    public override int Cost { get; init; } = 45;
    public override Func<DateTime, bool> IsRewardEnabled { get; set; } = time => _isRewardEnabled;

    private static bool _isRewardEnabled = false;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            twitchClient.OnMessageReceived += OnMessageReceived;
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
            logger.LogInformation(
                "RandomBadAppleDay запущен. Награда может активироваться с вероятностью {Chance}% на каждое слово.",
                ActivationChancePerWord * 100
            );
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            twitchClient.OnMessageReceived -= OnMessageReceived;
            wsClient.ChannelPointsCustomRewardRedemptionAdd -=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        return Task.CompletedTask;
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Payload.Event;

        var cost = twEvent.Reward.Cost;
        var channel = twEvent.BroadcasterUserId;

        if (
            channel.Equals(TwitchExstension.ChannelId, StringComparison.OrdinalIgnoreCase)
            && cost == Cost
        )
        {
            await Task.Run(async () =>
            {
                _isRewardEnabled = false;
                var videoFiles = GetAvailableVideoFiles();
                if (videoFiles.Count > 0)
                {
                    TimerElapseNow();
                    var randomVideo = videoFiles[_random.Next(videoFiles.Count)];
                    await SendAlertAsync(randomVideo);
                }
            });
        }
    }

    public override async Task StopAsync(CancellationToken cancelToken)
    {
        twitchClient.OnMessageReceived -= OnMessageReceived;
        await base.StopAsync(cancelToken);
    }

    /// <summary>
    /// Ручная активация награды (для команды)
    /// </summary>
    public async Task<string> ManualActivateAsync()
    {
        // Получаем видео файлы
        var videoFiles = GetAvailableVideoFiles();

        if (videoFiles.Count == 0)
        {
            return "❌ Нет доступных видео файлов Bad Apple";
        }

        // Выбираем случайное видео
        var randomVideo = videoFiles[_random.Next(videoFiles.Count)];

        // Активируем награду от имени администратора
        await ActivateRewardAsync("Admin", "Администратор", randomVideo);

        return $"✅ Bad Apple активирован! Видео: {Path.GetFileName(randomVideo)}";
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
        for (var index = 0; index < words.Length; index++)
        {
            if (_random.NextDouble() < ActivationChancePerWord)
            {
                // Вероятность сработала - проверяем есть ли видео файлы
                var videoFiles = GetAvailableVideoFiles();

                if (videoFiles.Count > 0)
                {
                    // Выбираем случайное видео
                    var randomVideo = videoFiles[_random.Next(videoFiles.Count)];
                    await ActivateRewardAsync(
                        e.ChatMessage.Username,
                        e.ChatMessage.DisplayName,
                        randomVideo
                    );
                }

                return; // Активируем только один раз за сообщение
            }
        }
    }

    private List<string> GetAvailableVideoFiles()
    {
        try
        {
            var badApplesPath = Path.Combine(environment.WebRootPath, BadApplesFolder);

            if (!Directory.Exists(badApplesPath))
            {
                return [];
            }

            var videoExtensions = new[] { ".mp4", ".webm" };
            var videoFiles = Directory
                .GetFiles(badApplesPath)
                .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
                .Select(f => Path.Combine(BadApplesFolder, Path.GetFileName(f)))
                .ToList();

            return videoFiles;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении видео из папки {BadApplesFolder}",
                BadApplesFolder
            );
            return [];
        }
    }

    private async Task ActivateRewardAsync(string username, string displayName, string videoPath)
    {
        try
        {
            _lastActivation = DateTime.Now;

            // Отправляем сообщение в чат
            var duration = TimeSpan.FromMinutes(10);
            var endTime = DateTime.Now.Add(duration);
            var timeString = endTime.ToString("HH:mm");

            var chatMessage =
                $"💀 {displayName} случайным образом активировал(а) {AlertDisplayName}! Награда будет доступна до {timeString}! 💀";

            _isRewardEnabled = true;

            TimerElapseNow();
            await twitchClient.SendMessageToMainTwitchAsync(chatMessage, logger);

            await Task.Factory.StartNew(async () =>
            {
                await Task.Delay(duration);

                if (!_isRewardEnabled)
                {
                    logger.LogInformation(
                        "RandomBadAppleDay деактивирована после 10 минут активации пользователем {Username} ({DisplayName}). Видео: {Video}",
                        username,
                        displayName,
                        videoPath
                    );
                }
                else
                {
                    _isRewardEnabled = false;
                }
            });

            logger.LogInformation(
                "RandomBadAppleDay активирована пользователем {Username} ({DisplayName}). Видео: {Video}",
                username,
                displayName,
                videoPath
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
