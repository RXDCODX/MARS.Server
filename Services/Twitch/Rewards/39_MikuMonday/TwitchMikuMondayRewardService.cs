using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._39_MikuMonday;

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
    IHostEnvironment environment,
    RickRollerService rickRollerService,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎤 Miku Monday [BETA TEST]";

    public override string AlertDescription { get; set; } =
        "🎵 Мику заметила нас и решила посетить этот стрим! Активируй награду и получи от неё персональный трек! Один раз - каждый понедельник! ♪";

    public override Color Color { get; set; } = Color.FromArgb(57, 197, 187); // Светло-салатовый/бирюзовый цвет Hatsune Miku

    public override int Cost { get; init; } = 39; // 39 - отсылка к числу Мику (3/9 - 9 марта)

    // Награда доступна только по понедельникам
    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek == DayOfWeek.Monday;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Инициализируем треки из JSON
        await tracksService.InitializeTracksAsync();

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

        await base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancelToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        return base.StopAsync(cancelToken);
    }

    private async Task OnChannelPointsCustomRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireCost(Cost)
            .RequireFollower()
            .ValidateWithResponseAsync(args.Payload.Event.UserName);

        if (vr.IsInvalid)
        {
            return;
        }

        var twEvent = args.Payload.Event;

        try
        {
            await rickRollerService.TryRickRollAsync(
                TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                async () =>
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
                        : await tracksService.GetRandomTrackForUserAsync(
                            twEvent.UserId,
                            twEvent.UserName
                        );

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
                            trackResult.Track.BaseTrackInfo?.Authors?.FirstOrDefault()
                            ?? "Unknown Artist",
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

                    var trackArtist =
                        trackResult.Track.BaseTrackInfo?.Authors?.FirstOrDefault() ?? "Unknown";
                    var trackTitle = trackResult.Track.BaseTrackInfo?.TrackName ?? "Unknown";

                    logger.LogInformation(
                        "Miku Monday эффект активирован для {UserType} {UserName}, трек: #{Number} {Artist} - {Title}",
                        isStreamer ? "стримера" : "пользователя",
                        twEvent.UserName,
                        trackResult.Track.Number,
                        trackArtist,
                        trackTitle
                    );
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }
}
