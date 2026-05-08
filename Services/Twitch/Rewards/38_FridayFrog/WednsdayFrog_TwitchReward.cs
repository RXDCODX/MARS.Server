using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._38_FridayFrog;

public class WednsdayFrog_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<WednsdayFrog_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🐸 Жабья среда";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.DarkOliveGreen;
    public override int Cost { get; init; } = 38;
    public override Func<bool> IsRewardEnabled { get; set; } =
        () => DateTime.Now.DayOfWeek == DayOfWeek.Wednesday;
}
