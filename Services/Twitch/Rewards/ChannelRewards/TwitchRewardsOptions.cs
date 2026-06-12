using System.Collections.Generic;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public class TwitchRewardsOptions
{
    public const string SectionName = "TwitchRewards";

    // Ключ — стоимость награды, значение — включена ли награда
    public Dictionary<int, bool> EnabledByCost { get; set; } = new();
}
