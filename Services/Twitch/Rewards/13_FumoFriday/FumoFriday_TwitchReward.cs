using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards._13_FumoFriday.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._13_FumoFriday;

public class FumoFriday_TwitchReward(
    ChannelRewardsService channelRewardsService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<FumoFriday_TwitchReward> logger,
    IHostApplicationLifetime hostApplicationLifetime,
    ITwitchClient twitchClient,
    ITwitchAPI twitchApi,
    EventSubWebsocketClient wsClient,
    TwitchUserEnsureService twitchUserEnsureService,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    IHostEnvironment environment,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🌙 Fumo Friday";

    public override string AlertDescription { get; set; } =
        "Присоединись к Fumo Friday и получи персональное сообщение от фумо каждую пятницу! ♪";

    public override Color Color { get; set; } = Color.FromArgb(255, 192, 203); // Розовый цвет для Fumo

    public override int Cost { get; init; } = 13;

    // Награда доступна только по пятницам
    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek == DayOfWeek.Friday;

    public bool IsServiceActive { get; set; } = true;

    private readonly CancellationToken _cancellationToken =
        hostApplicationLifetime.ApplicationStopping;

    private readonly List<string> _users = [];

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);

        hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            twitchClient.OnMessageReceived += OnMessageReceived;
            wsClient.ChannelPointsCustomRewardRedemptionAdd += OnRewardRedemption;
        });

        hostApplicationLifetime.ApplicationStopping.Register(() =>
        {
            twitchClient.OnMessageReceived -= OnMessageReceived;
            wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnRewardRedemption;
        });
    }

    public override async Task StopAsync(CancellationToken cancelToken)
    {
        twitchClient.OnMessageReceived -= OnMessageReceived;
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnRewardRedemption;

        await base.StopAsync(cancelToken);
    }

    public async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var vr = await validator
            .ForMessageReceived(e)
            .RequireServiceActive(IsServiceActive)
            .SkipBlacklisted()
            .RequireFollower()
            .ValidateWithResponseAsync(e.ChatMessage.Username);

        if (vr.IsInvalid)
        {
            return;
        }

        var name = e.ChatMessage.DisplayName;
        var id = e.ChatMessage.UserId;
        var colorHex = e.ChatMessage.HexColor;
        var now = DateTimeOffset.Now;

        if (!_users.Contains(id) && e.ChatMessage.Channel == TwitchExstension.Channel)
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                        _cancellationToken
                    );

                    var fumoUser = await dbContext.FumoUsers.FindAsync([id], _cancellationToken);

                    if (
                        fumoUser != null
                        && now - fumoUser.LastTime > TimeSpan.FromHours(24)
                        && now.DayOfWeek == DayOfWeek.Friday
                    )
                    {
                        var color = string.IsNullOrWhiteSpace(colorHex)
                            ? (await GetColor(id))
                            : colorHex;
                        await alertsHub.Clients.All.FumoFriday(name, color);
                        _users.Add(id);

                        fumoUser.LastTime = now;
                        await dbContext.SaveChangesAsync(_cancellationToken);
                    }
                },
                _cancellationToken
            );
        }
    }

    public async Task OnRewardRedemption(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireServiceActive(IsServiceActive)
            .RequireBroadcasterUserLogin()
            .ValidateWithResponseAsync(args.Payload.Event.UserName);

        if (vr.IsInvalid)
        {
            return;
        }

        await Task.Factory.StartNew(
            async () =>
            {
                if (args.Payload.Event.Reward.Cost == Cost)
                {
                    var name = args.Payload.Event.UserName;
                    var id = args.Payload.Event.UserId;

                    if (_users.Contains(name))
                    {
                        await twitchClient.SendMessageToMainTwitchAsync(
                            "Ты уже подписан на Fumo Friday",
                            logger
                        );
                        return;
                    }

                    try
                    {
                        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                            _cancellationToken
                        );
                        var now = DateTimeOffset.Now;

                        var isExists = await dbContext.FumoUsers.AnyAsync(
                            e => e.TwitchId == id,
                            _cancellationToken
                        );

                        if (!isExists)
                        {
                            // Гарантируем наличие пользователя в TwitchUsers перед созданием FumoUser
                            await twitchUserEnsureService.EnsureUserExistsAsync(
                                args,
                                _cancellationToken
                            );

                            var host = new FumoUser { TwitchId = id, LastTime = now };

                            await dbContext.FumoUsers.AddAsync(host, _cancellationToken);
                            await dbContext.SaveChangesAsync(_cancellationToken);

                            if (now.DayOfWeek == DayOfWeek.Friday)
                            {
                                var color = await GetColor(id);
                                await alertsHub.Clients.All.FumoFriday(name, color);
                                _users.Add(id);
                            }
                        }
                        else
                        {
                            await twitchClient.SendMessageToMainTwitchAsync(
                                $"@{name}, Ты уже счастливый фанат фум!",
                                logger
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogException(ex);
                    }
                }
            },
            _cancellationToken
        );
    }

    private async Task<string?> GetColor(string id)
    {
        try
        {
            var aa = await twitchApi.Helix.Chat.GetUserChatColorAsync([id]);
            return aa.Data[0].Color;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
