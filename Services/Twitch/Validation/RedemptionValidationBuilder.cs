using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.TwitchFollowers;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Validation;

public sealed class RedemptionValidationBuilder(
    ChannelPointsCustomRewardRedemptionArgs args,
    FollowerDbService followerDb
) : IRedemptionValidationBuilder
{
    private readonly List<Func<Task>> _checks = [];
    private readonly ValidationResult _result = new();

    private ChannelPointsCustomRewardRedemption Event => args.Payload.Event;

    public IRedemptionValidationBuilder RequireBroadcasterUserId()
    {
        _checks.Add(() =>
        {
            if (
                !Event.BroadcasterUserId.Equals(
                    TwitchExstension.ChannelId,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new ValidationException("Награда не относится к этому каналу");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IRedemptionValidationBuilder RequireBroadcasterUserLogin()
    {
        _checks.Add(() =>
        {
            if (
                !Event.BroadcasterUserLogin.Equals(
                    TwitchExstension.Channel,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new ValidationException("Награда не относится к этому каналу");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IRedemptionValidationBuilder RequireCost(int cost)
    {
        _checks.Add(() =>
        {
            if (Event.Reward.Cost != cost)
            {
                throw new ValidationException("Неверная стоимость награды");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IRedemptionValidationBuilder RequireServiceActive(bool isActive)
    {
        _checks.Add(() =>
        {
            if (!isActive)
            {
                throw new ValidationException("Сервис временно неактивен");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IRedemptionValidationBuilder RequireRewardEnabled(Func<bool> isEnabled)
    {
        _checks.Add(() =>
        {
            if (!isEnabled())
            {
                throw new ValidationException("Награда временно отключена");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IRedemptionValidationBuilder RequireRewardGuid(Guid? expected)
    {
        _checks.Add(() =>
        {
            if (!expected.HasValue)
            {
                throw new ValidationException("Награда не настроена");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IRedemptionValidationBuilder RequireFollower()
    {
        _checks.Add(async () =>
        {
            var userId = Event.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ValidationException("Не удалось проверить подписку");
            }

            var follower = await followerDb.GetFollowerFromDbAsync(userId);
            if (follower == null)
            {
                throw new ValidationException("Подпишись на канал, чтобы использовать эту награду");
            }
        });

        return this;
    }

    public async Task<ValidationResult> ValidateAsync()
    {
        var result = new ValidationResult();

        foreach (var check in _checks)
        {
            try
            {
                await check();
            }
            catch (ValidationException ex)
            {
                result.AddError(ex.Message);
            }
        }

        return result;
    }
}
