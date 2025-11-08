using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Rewards.TwitchMikuMondayReward.Entities;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchMikuMondayReward;

/// <summary>
/// Временная награда "Miku Monday" - доступна только по понедельникам
/// </summary>
public class TwitchMikuMondayRewardService(
    ChannelRewardsService channelRewardsService,
    TwitchUserEnsureService twitchUserEnsureService,
    ILogger<TwitchMikuMondayRewardService> logger,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    MikuMondayTracksService tracksService,
    ITwitchClient twitchClient,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎤 Miku Monday";

    public override string AlertDescription { get; set; } =
        "Мику заметила нас и решила посетить этот стрим! Активируй награду и получи от неё персональный трек! Один раз - каждый понедельник! ♪";

    public override Color Color { get; set; } = Color.FromArgb(57, 197, 187); // Светло-салатовый/бирюзовый цвет Hatsune Miku

    public override int Cost { get; init; } = 39; // 39 - отсылка к числу Мику (3/9 - 9 марта)

    public override Func<DateTime, bool> IsRewardEnabled { get; set; } =
        date =>
        {
            // Награда доступна только по понедельникам
            var result = date.DayOfWeek == DayOfWeek.Monday;
            return result;
        };

    public bool IsServiceActive { get; set; } = true;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Инициализируем треки из JSON
        await tracksService.InitializeTracksAsync();

        await base.StartAsync(cancellationToken);

        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                OnChannelPointsCustomRewardRedemption;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd -=
                OnChannelPointsCustomRewardRedemption;
        });
    }

    public override async Task StopAsync(CancellationToken cancelToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;

        await base.StopAsync(cancelToken);
    }

    private async Task OnChannelPointsCustomRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (!IsServiceActive)
        {
            return;
        }

        var twEvent = args.Payload.Event;

        // Проверяем, что это награда за нужное количество баллов и от нужного канала
        if (
            twEvent.Reward.Cost != Cost
            || !twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        try
        {
            logger.LogInformation(
                "Miku Monday награда активирована пользователем {UserName} за {Cost} баллов",
                twEvent.UserName,
                twEvent.Reward.Cost
            );

            var isStreamer = string.Equals(
                twEvent.UserId,
                twEvent.BroadcasterUserId,
                StringComparison.Ordinal
            );

            // Получаем случайный трек
            var trackResult = isStreamer
                ? await tracksService.GetRandomTrackForStreamerAsync()
                : await tracksService.GetRandomTrackForUserAsync(twEvent.UserId, twEvent.UserName);

            // Если есть ошибка - отправляем сообщение в чат
            if (!string.IsNullOrWhiteSpace(trackResult.Error))
            {
                if (isStreamer)
                {
                    logger.LogWarning(
                        "Miku Monday: ошибка активации стримером {UserName}: {Error}",
                        twEvent.UserName,
                        trackResult.Error
                    );
                }
                else
                {
                    await twitchClient.SendMessageToMainTwitchAsync(
                        $"@{twEvent.UserName}, {trackResult.Error}",
                        logger
                    );
                    logger.LogWarning(
                        "Miku Monday: ошибка для пользователя {UserName}: {Error}",
                        twEvent.UserName,
                        trackResult.Error
                    );
                }
                return;
            }

            // Если трек не получен
            if (trackResult.Track == null)
            {
                if (isStreamer)
                {
                    logger.LogError(
                        "Miku Monday: не удалось получить трек для стримера {UserName}",
                        twEvent.UserName
                    );
                }
                else
                {
                    await twitchClient.SendMessageToMainTwitchAsync(
                        $"@{twEvent.UserName}, не удалось попросить Мику о благославлении! Попробуйте позже.",
                        logger
                    );
                    logger.LogError(
                        "Miku Monday: не удалось получить трек для пользователя {UserName}",
                        twEvent.UserName
                    );
                }
                return;
            }

            // Конвертируем в DTO
            var selectedTrackDto = new MikuTrackDto
            {
                Id = trackResult.Track.BaseTrackInfoId,
                Number = trackResult.Track.Number,
                Artist =
                    trackResult.Track.BaseTrackInfo?.Authors?.FirstOrDefault() ?? "Unknown Artist",
                Title = trackResult.Track.BaseTrackInfo?.TrackName ?? "Unknown Title",
                Url = trackResult.Track.BaseTrackInfo?.Url.ToString() ?? "",
                ThumbnailUrl = trackResult.Track.BaseTrackInfo?.ArtworkUrl?.ToString(),
            };

            var availableTracksDto = trackResult
                .AvailableTracks.Select(t => new MikuTrackDto
                {
                    Id = t.BaseTrackInfoId,
                    Number = t.Number,
                    Artist = t.BaseTrackInfo?.Authors?.FirstOrDefault() ?? "Unknown Artist",
                    Title = t.BaseTrackInfo?.TrackName ?? "Unknown Title",
                    Url = t.BaseTrackInfo?.Url.ToString() ?? "",
                    ThumbnailUrl = t.BaseTrackInfo?.ArtworkUrl?.ToString(),
                })
                .ToList();

            var twUser = TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args);

            twUser = await twitchUserEnsureService.EnsureUserExistsAsync(twUser);

            var mikuMondayData = new MikuMondayDto
            {
                Id = Guid.NewGuid(),
                TwitchUser = twUser,
                SelectedTrack = selectedTrackDto,
                AvailableTracks = availableTracksDto,
                SkipAvailableTracksUpdate = isStreamer,
            };

            // Отправляем данные на фронт
            await hubContext.Clients.All.MikuMonday(mikuMondayData);

            // Отправляем сообщение в чат
            var trackArtist =
                trackResult.Track.BaseTrackInfo?.Authors?.FirstOrDefault() ?? "Unknown";
            var trackTitle = trackResult.Track.BaseTrackInfo?.TrackName ?? "Unknown";

            //await twitchClient.SendMessageToMainTwitchAsync(
            //    $"@{twEvent.UserName} получил трек #{trackResult.Track.Number}: {trackArtist} - {trackTitle} 🎤 Осталось треков: {trackResult.AvailableTracks.Count}",
            //    logger
            //);

            logger.LogInformation(
                "Miku Monday эффект активирован для {UserType} {UserName}, трек: #{Number} {Artist} - {Title}",
                isStreamer ? "стримера" : "пользователя",
                twEvent.UserName,
                trackResult.Track.Number,
                trackArtist,
                trackTitle
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }
}
