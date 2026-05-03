using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;

namespace MARS.Server.Services.Twitch.Rewards._1702_EmojisReward;

public class Emojis_TwitchReward(
	ChannelRewardsService channelRewardsService,
	ILogger<Emojis_TwitchReward> logger,
	IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
	public override string AlertDisplayName { get; set; } = "Эмодзи!";

	public override string AlertDescription { get; set; } = "Выстрелить в экран указанными тобой смайликами! Есть поддержка Twitch BTTV 7TV FFZ смайликов! В РФ банят некоторые смайлы, возможно не будет работать 7тв bttv ffz.";

	public override Color Color { get; set; } = Color.FromArgb(31, 255, 72);

	public override int Cost { get; init; } = 1702;

	public override Func<DateTime, bool> IsRewardEnabled { get; set; } = _ => true;
}
