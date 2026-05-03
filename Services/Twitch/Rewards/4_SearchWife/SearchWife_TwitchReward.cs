using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._4_SearchWife;

public class SearchWife_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<SearchWife_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Поиск супруга";
    public override string AlertDescription { get; set; } = "Цена - 50 кредитов. Узнать кредиты - !rank/!myrank.";
    public override Color Color { get; set; } = Color.FromArgb(24, 0, 255);
    public override int Cost { get; init; } = 4;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
