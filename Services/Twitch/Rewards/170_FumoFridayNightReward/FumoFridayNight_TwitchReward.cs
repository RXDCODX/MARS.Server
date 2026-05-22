using System.Drawing;
using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._170_FumoFridayNightReward;

public class FumoFridayNight_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<FumoFridayNight_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    private protected override CreateCustomRewardsRequest CreateCustomRewardsRequest =>
        new()
        {
            Title = AlertDisplayName,
            Prompt = AlertDescription,
            Cost = Cost,
            IsEnabled = true,
            IsUserInputRequired = false,
            IsMaxPerStreamEnabled = false,
            IsMaxPerUserPerStreamEnabled = false,
            IsGlobalCooldownEnabled = true,
            ShouldRedemptionsSkipRequestQueue = false,
            GlobalCooldownSeconds = 180,
        };

    public override string AlertDisplayName { get; set; } = "🧸 Fumo Friday Night";
    public override string AlertDescription { get; set; } =
        "🎪 Твоя уникальная (ну почти) возможность активации Fumo Friday Night";
    public override Color Color { get; set; } = Color.Red;
    public override int Cost { get; init; } = 170;
    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek == DayOfWeek.Friday;
}
