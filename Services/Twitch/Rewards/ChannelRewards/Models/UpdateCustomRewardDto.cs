using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;

namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards.Models;

public sealed class UpdateCustomRewardDto
{
    public string? Title { get; set; }
    public int? Cost { get; set; }
    public bool? IsEnabled { get; set; }
    public string? Prompt { get; set; }
    public string? BackgroundColor { get; set; }
    public bool? IsUserInputRequired { get; set; }
    public bool? IsMaxPerStreamEnabled { get; set; }
    public int? MaxPerStream { get; set; }
    public bool? IsMaxPerUserPerStreamEnabled { get; set; }
    public int? MaxPerUserPerStream { get; set; }
    public bool? IsGlobalCooldownEnabled { get; set; }
    public int? GlobalCooldownSeconds { get; set; }
    public bool? ShouldRedemptionsSkipRequestQueue { get; set; }

    internal UpdateCustomRewardRequest ToUpdateRequest()
    {
        return new UpdateCustomRewardRequest
        {
            Title = Title,
            Cost = Cost,
            IsEnabled = IsEnabled,
            Prompt = Prompt,
            BackgroundColor = BackgroundColor,
            IsUserInputRequired = IsUserInputRequired,
            IsMaxPerStreamEnabled = IsMaxPerStreamEnabled,
            MaxPerStream = MaxPerStream,
            IsMaxPerUserPerStreamEnabled = IsMaxPerUserPerStreamEnabled,
            MaxPerUserPerStream = MaxPerUserPerStream,
            IsGlobalCooldownEnabled = IsGlobalCooldownEnabled,
            GlobalCooldownSeconds = GlobalCooldownSeconds,
            ShouldRedemptionsSkipRequestQueue = ShouldRedemptionsSkipRequestQueue,
        };
    }
}
