using System;
using System.Drawing;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards._5_AddWife;

public class AddWife_TwitchReward(
    ChannelRewardsService channelRewardsService,
    ILogger<AddWife_TwitchReward> logger,
    IHostEnvironment environment
) : TemporaryReward(channelRewardsService, logger, environment)
{
    public override string AlertDisplayName { get; set; } = "👰 Добавить супруга";
    public override string AlertDescription { get; set; } =
        "✨ Есть шанс получить VIP если выбить число >= 95! (Гарант на 200 крутках)"
        + Environment.NewLine
        + "Добавляет супруга в рулетку, ссылка на персонажа должна быть с https://shikimori.one/characters. "
        + Environment.NewLine
        + " Пример: https://shikimori.one/characters/723-nami";
    public override Color Color { get; set; } = Color.FromArgb(0, 30, 255);
    public override int Cost { get; init; } = 5;
    public override Func<bool> IsRewardEnabled { get; set; } =
        () =>
            DateTime.Now.DayOfWeek != DayOfWeek.Friday
            && DateTime.Now.DayOfWeek != DayOfWeek.Wednesday;

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
