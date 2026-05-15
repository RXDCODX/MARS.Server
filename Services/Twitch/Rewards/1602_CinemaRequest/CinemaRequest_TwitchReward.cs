using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards._1602_CinemaRequest;

public class CinemaRequest_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<CinemaRequest_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎬 CINEMA/ANIME REQUEST";
    public override string AlertDescription { get; set; } = "🎥 Ссылку на кинопоиск/шикимори";
    public override Color Color { get; set; } = Color.FromArgb(255, 130, 128);
    public override int Cost { get; init; } = 1602;
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
