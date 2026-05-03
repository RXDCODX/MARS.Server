using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._353_TikTokEdit;

public class TikTokEdit_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<TikTokEdit_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Make a TikTok Edit";
    public override string AlertDescription { get; set; } = "текст наверху и внизу можно разделить символом `=`";
    public override Color Color { get; set; } = Color.FromArgb(245, 0, 0);
    public override int Cost { get; init; } = 353;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
