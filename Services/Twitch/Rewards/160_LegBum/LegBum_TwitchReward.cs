using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards._160_LegBum;

public class LegBum_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<LegBum_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "�발 НОГОЙ БОМЖА";
    public override string AlertDescription { get; set; } =
        "🦵 Возращает баллы за использование если топтать asp/асп'a (аспиранта)";
    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);
    public override int Cost { get; init; } = 160;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
    private protected override CreateCustomRewardsRequest CreateCustomRewardsRequest
    {
        get
        {
            var values = base.CreateCustomRewardsRequest;
            values.IsUserInputRequired = true;
            return values;
        }
    }
}
