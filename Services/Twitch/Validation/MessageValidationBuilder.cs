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
                throw new ValidationException("Эта команда работает только в основном канале");
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
                throw new ValidationException("Эта команда доступна только стримеру");
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
                throw new ValidationException("У вас нет доступа к этой команде");
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
                throw new ValidationException("Не удалось определить награду");
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
                throw new ValidationException("Награда не настроена");
            }

            var rewardId = args.ChatMessage.CustomRewardId;
            if (string.IsNullOrWhiteSpace(rewardId) || Guid.Parse(rewardId) != expected)
            {
                throw new ValidationException("Награда не найдена");
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
                throw new ValidationException("Сервис временно неактивен");
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
                throw new ValidationException("Не удалось определить пользователя");
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
                throw new ValidationException("Не удалось проверить подписку");
            }

            var follower = await followerDb.GetFollowerFromDbAsync(userId);
            if (follower == null)
            {
                throw new ValidationException("Подпишись на канал, чтобы использовать эту команду");
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
