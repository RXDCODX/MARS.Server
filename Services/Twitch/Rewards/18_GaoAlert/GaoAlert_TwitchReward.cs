using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
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
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._18_GaoAlert;

public class GaoAlert_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<GaoAlert_TwitchReward> logger,
    IHostEnvironment environment,
    ITwitchAPI api,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    EventSubWebsocketClient wsClient,
    IHostApplicationLifetime lifetime,
    RickRollerService rickRollerService,
    ITwitchClient client,
    ITwitchEventValidationService validator
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🦁 GAO Alert";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(255, 255, 0);
    public override int Cost { get; init; } = 18;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemption;
        await base.StopAsync(cancellationToken);
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
            .ValidateAsync();

        if (vr.IsInvalid)
        {
            await client.SendMessageToMainTwitchAsync($"@{args.Payload.Event.UserName}, " + vr.FirstError);
            return;
        }

        var twEvent = args.Payload.Event;
        var text = args.Payload.Event.UserInput;

        await Task.Factory.StartNew(async () =>
        {
            try
            {
                await rickRollerService.TryRickRollAsync(
                    TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                    async () =>
                    {
                        text = text.Trim();
                        var isJustText = text.Contains(' ');

                        GaoAlertDto? gaoAlert;
                        if (!isJustText)
                        {
                            text = text.StartsWith('@') ? text.Substring(1) : text;

                            var isValidTwitchUsername =
                                text.Length <= 25 && Regex.IsMatch(text, @"^[a-zA-Z0-9_]+$");

                            if (isValidTwitchUsername)
                            {
                                try
                                {
                                    var twitchUser = await api.Helix.Users.GetUsersAsync(
                                        null,
                                        [text]
                                    );
                                    if (twitchUser is { Users.Length: > 0 })
                                    {
                                        var user = twitchUser.Users.First();
                                        gaoAlert = new GaoAlertDto
                                        {
                                            TwitchUser = user,
                                            IsJustText = false,
                                        };
                                        await hubContext.Clients.All.GaoAlert(gaoAlert);
                                        logger.LogInformation(
                                            "Gao alert with user {userName}",
                                            user.DisplayName
                                        );
                                        return;
                                    }
                                }
                                catch
                                {
                                    //ignored
                                }
                            }
                        }

                        gaoAlert = new GaoAlertDto { IsJustText = true, JustText = text };
                        await hubContext.Clients.All.GaoAlert(gaoAlert);
                        logger.LogInformation("Gao alert with user {text}", text);
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        });
    }
}
