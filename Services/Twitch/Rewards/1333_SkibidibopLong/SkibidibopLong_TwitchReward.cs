using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Rewards._1333_SkibidibopLong;

public class SkibidibopLong_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<SkibidibopLong_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎺 SKIBIDIBOP LONG";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(245, 0, 0);
    public override int Cost { get; init; } = 1333;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
