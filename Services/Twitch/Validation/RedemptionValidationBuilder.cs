using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.TwitchFollowers;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace MARS.Server.Services.Twitch.Validation;

public sealed class RedemptionValidationBuilder(
    ChannelPointsCustomRewardRedemptionArgs args,
    FollowerDbService followerDb,
    ITwitchClient client,
    ILogger logger,
    TwitchUserEnsureService userEnsureService,
    ConcurrentDictionary<string, DateTime> sentEventErrors
) : IRedemptionValidationBuilder
{
    private readonly List<(Func<Task> check, bool loud)> _checks = [];

    private ChannelPointsCustomRewardRedemption Event => args.Payload.Event;

    public IRedemptionValidationBuilder RequireBroadcasterUserId(bool loud = false)
    {
        _checks.Add(
            (
                () =>
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
                },
                loud
            )
        );

        return this;
    }

    public IRedemptionValidationBuilder RequireBroadcasterUserLogin(bool loud = false)
    {
        _checks.Add(
            (
                () =>
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
                },
                loud
            )
        );

        return this;
    }

    public IRedemptionValidationBuilder RequireCost(int cost, bool loud = false)
    {
        _checks.Add(
            (
                () =>
                {
                    if (Event.Reward.Cost != cost)
                    {
                        throw new ValidationException("Неверная стоимость награды");
                    }

                    return Task.CompletedTask;
                },
                loud
            )
        );

        return this;
    }

    public IRedemptionValidationBuilder RequireServiceActive(bool isActive, bool loud = false)
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

    public IRedemptionValidationBuilder RequireRewardEnabled(
        Func<bool> isEnabled,
        bool loud = false
    )
    {
        _checks.Add(
            (
                () =>
                {
                    if (!isEnabled())
                    {
                        throw new ValidationException("Награда временно отключена");
                    }

                    return Task.CompletedTask;
                },
                loud
            )
        );

        return this;
    }

    public IRedemptionValidationBuilder RequireRewardGuid(Guid? expected, bool loud = false)
    {
        _checks.Add(
            (
                () =>
                {
                    if (!expected.HasValue)
                    {
                        throw new ValidationException("Награда не настроена");
                    }

                    return Task.CompletedTask;
                },
                loud
            )
        );

        return this;
    }

    public IRedemptionValidationBuilder RequireFollower(bool loud = true)
    {
        _checks.Add(
            (
                async () =>
                {
                    var userId = Event.UserId;

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
                            "Подпишись на канал, чтобы использовать эту награду"
                        );
                    }
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
            var key = Event.Id;

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
