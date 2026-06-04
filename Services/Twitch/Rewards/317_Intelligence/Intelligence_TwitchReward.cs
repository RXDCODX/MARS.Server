namespace MARS.Server.Services.Twitch.Rewards._317_Intelligence;

public class Intelligence_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Intelligence_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🧠 Интеллект";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);
    public override int Cost { get; init; } = 317;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
