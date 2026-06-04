namespace MARS.Server.Services.Twitch.Rewards._215_Bye;

public class Bye_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Bye_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "👋 Пока";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(0, 33, 255);
    public override int Cost { get; init; } = 215;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
