using System.Collections.Concurrent;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.TwitchFollowers;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.Twitch.Validation;

public sealed class MessageValidationBuilder(
    OnMessageReceivedArgs args,
    FollowerDbService followerDb,
    ITwitchClient client,
    ILogger logger,
    TwitchUserEnsureService userEnsureService,
    ConcurrentDictionary<string, DateTime> sentEventErrors
) : IMessageValidationBuilder
{
    private readonly List<(Func<Task> check, bool loud)> _checks = [];

    public IMessageValidationBuilder RequireChannel(bool loud = false)
    {
        _checks.Add(
            (
                () =>
                {
                    if (
                        !args.ChatMessage.Channel.Equals(
                            TwitchExstension.Channel,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        throw new ValidationException(
                            "Эта команда работает только в основном канале"
                        );
                    }

                    return Task.CompletedTask;
                },
                loud
            )
        );

        return this;
    }

    public IMessageValidationBuilder RequireBroadcasterId(bool loud = false)
    {
        _checks.Add(
            (
                () =>
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
                },
                loud
            )
        );

        return this;
    }

    public IMessageValidationBuilder SkipBlacklisted(bool loud = true)
    {
        _checks.Add(
            (
                () =>
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
                },
                loud
            )
        );

        return this;
    }

    public IMessageValidationBuilder RequireRewardId(bool loud = false)
    {
        _checks.Add(
            (
                () =>
                {
                    if (string.IsNullOrWhiteSpace(args.ChatMessage.CustomRewardId))
                    {
                        throw new ValidationException("Не удалось определить награду");
                    }

                    return Task.CompletedTask;
                },
                loud
            )
        );

        return this;
    }

    public IMessageValidationBuilder RequireRewardGuid(Guid? expected, bool loud = false)
    {
        _checks.Add(
            (
                () =>
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
                },
                loud
            )
        );

        return this;
    }

    public IMessageValidationBuilder RequireServiceActive(bool isActive, bool loud = false)
    {
        _checks.Add(
            (
                () =>
                {
                    if (!isActive)
                    {
                        throw new ValidationException("Сервис временно неактивен");
                    }

                    return Task.CompletedTask;
                },
                loud
            )
        );

        return this;
    }

    public IMessageValidationBuilder RequireUserId(bool loud = false)
    {
        _checks.Add(
            (
                () =>
                {
                    if (string.IsNullOrWhiteSpace(args.ChatMessage.UserId))
                    {
                        throw new ValidationException("Не удалось определить пользователя");
                    }

                    return Task.CompletedTask;
                },
                loud
            )
        );

        return this;
    }

    public IMessageValidationBuilder RequireFollower(bool loud = true)
    {
        _checks.Add(
            (
                async () =>
                {
                    var userId = args.ChatMessage.UserId;

                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        throw new ValidationException("Не удалось проверить подписку");
                    }

                    if (userId == TwitchExstension.ChannelId)
                    {
                        return;
                    }

                    try
                    {
                        var user = await userEnsureService.EnsureUserExistsAsync(userId);
                        if (user is { IsModerator: true } or { IsVip: true })
                        {
                            return;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // User not found in DB — treat as regular user
                    }

                    var follower = await followerDb.GetFollowerFromDbAsync(userId);
                    if (follower == null)
                    {
                        throw new ValidationException(
                            "Подпишись на канал, чтобы использовать эту команду"
                        );
                    }
                },
                loud
            )
        );

        return this;
    }

    public IMessageValidationBuilder IsReplyToBot(bool loud = true)
    {
        _checks.Add(
            (
                () =>
                {
                    var reply = args.ChatMessage.ChatReply;

                    // Если это не реплай — проверка пропускается (keyword match)
                    if (reply is null)
                    {
                        return Task.CompletedTask;
                    }

                    // Если это реплай, но НЕ на бота — ошибка
                    if (
                        !string.Equals(
                            reply.ParentUserLogin,
                            TwitchExstension.BotName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        throw new ValidationException("Реплай не на сообщение бота");
                    }

                    return Task.CompletedTask;
                },
                loud
            )
        );

        return this;
    }

    public async Task<ValidationResult> ValidateAsync()
    {
        var result = new ValidationResult();
        var silentFailed = false;

        foreach (var (check, loud) in _checks)
        {
            if (loud && silentFailed)
            {
                continue;
            }

            try
            {
                await check();
            }
            catch (ValidationException ex)
            {
                if (loud)
                {
                    result.AddError(ex.Message);
                }
                else
                {
                    silentFailed = true;
                    result.HasSilentFailure = true;
                }
            }
        }

        return result;
    }

    public async Task<ValidationResult> ValidateWithResponseAsync(string userName)
    {
        var result = await ValidateAsync();

        if (result is { IsInvalid: true, FirstError: not null })
        {
            var key = args.ChatMessage.Id;

            if (key is null || sentEventErrors.TryAdd(key, DateTime.Now))
            {
                try
                {
                    await client.SendMessageToMainTwitchAsync(
                        $"@{userName}, {result.FirstError}",
                        logger
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send validation error message");
                }

                if (key is not null)
                {
                    await Task.Factory.StartNew(
                        async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(30));
                            sentEventErrors.TryRemove(key, out _);
                        },
                        CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default
                    );
                }
            }
        }

        return result;
    }
}
