using System;
using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Rewards._6_RussianRoulette;

public class RussianRoulette_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<RussianRoulette_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🔫 Русская рулетка";
    public override string AlertDescription { get; set; } =
        "🎰 Есть вариант игр на 1 игрока, на 2 игроков и на множество до 8";
    public override Color Color { get; set; } = Color.FromArgb(247, 0, 255);
    public override int Cost { get; init; } = 6;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
