using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._3_SoundRequest;

public class SoundRequest_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<SoundRequest_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "SOUND REQUEST";
    public override string AlertDescription { get; set; } =
        "Прикрепите ссылку на YouTube. Баллы вернутся, если заказ не пройдет по фильтрам";
    public override Color Color { get; set; } = Color.FromArgb(34, 177, 227);
    public override int Cost { get; init; } = 3;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
