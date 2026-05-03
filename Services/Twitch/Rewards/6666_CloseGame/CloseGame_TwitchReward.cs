using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._6666_CloseGame;

public class CloseGame_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<CloseGame_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Выключить теккен";
    public override string AlertDescription { get; set; } = "Закрывает игрульку";
    public override Color Color { get; set; } = Color.FromArgb(0, 128, 255);
    public override int Cost { get; init; } = 6666;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
