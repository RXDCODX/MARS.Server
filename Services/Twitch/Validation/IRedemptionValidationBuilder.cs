using System;
using System.Threading.Tasks;

namespace MARS.Server.Services.Twitch.Validation;

public interface IRedemptionValidationBuilder
{
    IRedemptionValidationBuilder RequireBroadcasterUserId();
    IRedemptionValidationBuilder RequireBroadcasterUserLogin();
    IRedemptionValidationBuilder RequireCost(int cost);
    IRedemptionValidationBuilder RequireServiceActive(bool isActive);
    IRedemptionValidationBuilder RequireRewardEnabled(Func<bool> isEnabled);
    IRedemptionValidationBuilder RequireRewardGuid(Guid? expected);
    IRedemptionValidationBuilder RequireFollower();
    Task<ValidationResult> ValidateAsync();
}
