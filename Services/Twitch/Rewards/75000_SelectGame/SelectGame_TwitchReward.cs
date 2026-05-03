using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._75000_SelectGame;

public class SelectGame_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<SelectGame_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "Выберите мне игру";
    public override string AlertDescription { get; set; } = "Выберите, во что я буду играть дальше. Если этой игры нету у меня в стиме - то отмена и баллы не возращаются (можно спросить заранее)";
    public override Color Color { get; set; } = Color.FromArgb(190, 250, 225);
    public override int Cost { get; init; } = 75000;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
