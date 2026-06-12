using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Rewards._9_AudioQuiz;

public class AudioQuiz_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<AudioQuiz_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "🎵 Аудио викторина";
    public override string AlertDescription { get; set; } =
        "🔊 Нужно угадать трек по фрагменту. В это время SoundRequest ставится на паузу";
    public override Color Color { get; set; } = Color.FromArgb(247, 0, 255);
    public override int Cost { get; init; } = 9;
    public override Func<bool> IsRewardEnabled { get; set; } = () => false;
}
