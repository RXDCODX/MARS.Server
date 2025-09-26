using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Models;

namespace MARS.Server.Services.Twitch.Rewards.TestReward;

/// <summary>
/// Определение тестовой награды для проверки перехвата активации
/// </summary>
public class TestRewardDefinition : ChannelRewardDefinition
{
    public TestRewardDefinition()
    {
        Title = "Тестовая награда";
        Cost = 88888;
        IsEnabled = true;
        Prompt = "Введите 'test' для тестирования отмены награды";
        BackgroundColor = "#FF6B6B";
        IsUserInputRequired = true;
        IsMaxPerStreamEnabled = false;
        IsMaxPerUserPerStreamEnabled = false;
        IsGlobalCooldownEnabled = false;
        ShouldRedemptionsSkipRequestQueue = false;
    }
}
