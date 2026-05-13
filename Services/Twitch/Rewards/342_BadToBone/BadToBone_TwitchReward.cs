using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards._342_BadToBone;

public class BadToBone_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<BadToBone_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎸 BAD TO THE BONE";
    public override string AlertDescription { get; set; } =
        "📝 текст наверху и внизу можно разделить символом `=`";
    public override Color Color { get; set; } = Color.FromArgb(255, 0, 0);
    public override int Cost { get; init; } = 342;
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
