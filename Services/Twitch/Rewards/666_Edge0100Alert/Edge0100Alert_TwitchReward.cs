using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._666_Edge0100Alert;

public class Edge0100Alert_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<Edge0100Alert_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "👑 Специальный алерт для Edge0100";
    public override string AlertDescription { get; set; } = string.Empty;
    public override Color Color { get; set; } = Color.FromArgb(235, 4, 0);
    public override int Cost { get; init; } = 666;
    public override Func<bool> IsRewardEnabled { get; set; } = () => true;
}
