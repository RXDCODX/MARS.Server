using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Rewards._134_Pedro;

public class Pedro_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Pedro_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "👻 PEDRO PEDRO PEDRO";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);
    public override int Cost { get; init; } = 134;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
