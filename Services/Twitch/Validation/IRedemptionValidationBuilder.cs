namespace MARS.Server.Services.Twitch.Validation;

public interface IRedemptionValidationBuilder
{
    IRedemptionValidationBuilder RequireBroadcasterUserId(bool loud = false);
    IRedemptionValidationBuilder RequireBroadcasterUserLogin(bool loud = false);
    IRedemptionValidationBuilder RequireCost(int cost, bool loud = false);
    IRedemptionValidationBuilder RequireServiceActive(bool isActive, bool loud = false);
    IRedemptionValidationBuilder RequireRewardEnabled(Func<bool> isEnabled, bool loud = false);
    IRedemptionValidationBuilder RequireRewardGuid(Guid? expected, bool loud = false);
    IRedemptionValidationBuilder RequireFollower(bool loud = true);
    Task<ValidationResult> ValidateAsync();
    Task<ValidationResult> ValidateWithResponseAsync(string userName);
}
