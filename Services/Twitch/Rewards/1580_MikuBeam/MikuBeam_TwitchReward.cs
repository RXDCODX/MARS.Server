using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Rewards._1580_MikuBeam;

public class MikuBeam_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<MikuBeam_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "✨ MIKU MIKU BEAM";
    public override string AlertDescription { get; set; } =
        "🗑️ Удаляет в чате последние 100 сообщений!";
    public override Color Color { get; set; } = Color.FromArgb(255, 255, 255);
    public override int Cost { get; init; } = 1580;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
