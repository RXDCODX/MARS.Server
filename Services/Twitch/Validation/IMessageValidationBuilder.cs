using System;
using System.Threading.Tasks;

namespace MARS.Server.Services.Twitch.Validation;

public interface IMessageValidationBuilder
{
    IMessageValidationBuilder RequireChannel();
    IMessageValidationBuilder RequireBroadcasterId();
    IMessageValidationBuilder SkipBlacklisted();
    IMessageValidationBuilder RequireRewardId();
    IMessageValidationBuilder RequireRewardGuid(Guid? expected);
    IMessageValidationBuilder RequireServiceActive(bool isActive);
    IMessageValidationBuilder RequireUserId();
    IMessageValidationBuilder RequireFollower();
    Task<ValidationResult> ValidateAsync();
}
