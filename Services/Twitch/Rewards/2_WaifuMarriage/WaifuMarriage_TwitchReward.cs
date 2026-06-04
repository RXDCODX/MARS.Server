namespace MARS.Server.Services.Twitch.Rewards._2_WaifuMarriage;

public class WaifuMarriage_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<WaifuMarriage_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "💒 Свадьба";

    public override string AlertDescription { get; set; } =
        "💝 Свадьба с твоим будущим супругом раз и навсегда! Чтобы работало надо сначала попробовать поискать супруга за алерт за 4 балла канала! Развод с супругом только за 500р! Подумай дважды!";

    public override Color Color { get; set; } = Color.FromArgb(0, 18, 255);

    public override int Cost { get; init; } = 2;

    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
