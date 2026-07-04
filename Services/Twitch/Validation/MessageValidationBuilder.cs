using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.TwitchFollowers;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Validation;

public sealed class MessageValidationBuilder(
    OnMessageReceivedArgs args,
    FollowerDbService followerDb
) : IMessageValidationBuilder
{
    private readonly List<Func<Task>> _checks = [];

    public IMessageValidationBuilder RequireChannel()
    {
        _checks.Add(() =>
        {
            if (
                !args.ChatMessage.Channel.Equals(
                    TwitchExstension.Channel,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new ValidationException("Message is not from the main channel");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IMessageValidationBuilder RequireBroadcasterId()
    {
        _checks.Add(() =>
        {
            if (
                !args.ChatMessage.RoomId.Equals(
                    TwitchExstension.ChannelId,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new ValidationException("Message is not from the broadcaster");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IMessageValidationBuilder SkipBlacklisted()
    {
        _checks.Add(() =>
        {
            if (
                TwitchExstension.BlackList.Logins.Any(t =>
                    t.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                throw new ValidationException("User is in the blacklist");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IMessageValidationBuilder RequireRewardId()
    {
        _checks.Add(() =>
        {
            if (string.IsNullOrWhiteSpace(args.ChatMessage.CustomRewardId))
            {
                throw new ValidationException("Reward ID is empty");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IMessageValidationBuilder RequireRewardGuid(Guid? expected)
    {
        _checks.Add(() =>
        {
            if (!expected.HasValue)
            {
                throw new ValidationException("Reward GUID is not configured");
            }

            var rewardId = args.ChatMessage.CustomRewardId;
            if (string.IsNullOrWhiteSpace(rewardId) || Guid.Parse(rewardId) != expected)
            {
                throw new ValidationException("Reward GUID does not match");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IMessageValidationBuilder RequireServiceActive(bool isActive)
    {
        _checks.Add(() =>
        {
            if (!isActive)
            {
                throw new ValidationException("Service is not active");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IMessageValidationBuilder RequireUserId()
    {
        _checks.Add(() =>
        {
            if (string.IsNullOrWhiteSpace(args.ChatMessage.UserId))
            {
                throw new ValidationException("User ID is empty");
            }

            return Task.CompletedTask;
        });

        return this;
    }

    public IMessageValidationBuilder RequireFollower()
    {
        _checks.Add(async () =>
        {
            var userId = args.ChatMessage.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ValidationException("Cannot check follower status: User ID is empty");
            }

            var follower = await followerDb.GetFollowerFromDbAsync(userId);
            if (follower == null)
            {
                throw new ValidationException("User is not a follower");
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
