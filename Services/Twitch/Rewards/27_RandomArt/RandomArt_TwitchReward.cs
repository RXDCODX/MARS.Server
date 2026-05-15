using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards._27_RandomArt;

public class RandomArt_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<RandomArt_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎨 Random Art";
    public override string AlertDescription { get; set; } =
        "🖼️ введи любое число для рандомного арта. Если нужен по теме, Example: black_hair asuna_(sao) https://gelbooru.com/index.php?page=tags&s=list";
    public override Color Color { get; set; } = Color.FromArgb(145, 71, 255);
    public override int Cost { get; init; } = 27;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;

    private protected override CreateCustomRewardsRequest CreateCustomRewardsRequest
    {
        get
        {
            var value = base.CreateCustomRewardsRequest;
            value.IsUserInputRequired = true;
            return value;
        }
    }
}
