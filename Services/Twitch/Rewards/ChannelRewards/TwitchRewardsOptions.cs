namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards;

public class TwitchRewardsOptions
{
    public const string SectionName = "TwitchRewards";

    // Ключ — стоимость награды, значение — включена ли награда
    public Dictionary<int, bool> EnabledByCost { get; set; } = new();

    // Список Cost наград, исключённых из пула случайной активации (для награды за 1 балл)
    public int[] ExcludeFromRandomPool { get; set; } = [];
}
