using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.Twitch.Validation;
using MARS.Shared.Hubs;
using MARS.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchUser = MARS.Server.Services.Twitch.Entitys.TwitchUser;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class TtsHubBroadcaster(
    IHubContext<Hubs.VoiceRecognitionHub, IVoiceRecognitionHub> hubContext,
    IHubContext<
        Hubs.AudioControllerHub.AudioControllerHub,
        MARS.Shared.Hubs.IAudioControllerHub
    > audioHubContext,
    ILogger<TtsHubBroadcaster> logger,
    ITwitchClient client,
    IHostApplicationLifetime lifetime,
    ISevenTvEmoteService sevenTvEmoteService,
    ITtsMessageFilterService ttsMessageFilterService,
    TwitchUserEnsureService twitchUserEnsureService,
    ITwitchEventValidationService validator
) : BackgroundService, ITtsHubBroadcaster
{
    private const string TtsConsumersGroupName = "tts-consumers";
    private const string AudioControllersGroupName = "audio-controllers";
    private readonly Lock _stateGate = new();

    public double CurrentVolume
    {
        get
        {
            lock (_stateGate)
            {
                return field;
            }
        }
        private set;
    } = 1.0;

    public bool CurrentRelayToDiscord
    {
        get
        {
            lock (_stateGate)
            {
                return field;
            }
        }
        private set;
    }

    double ITtsHubBroadcaster.CurrentVolume => CurrentVolume;

    bool ITtsHubBroadcaster.CurrentRelayToDiscord => CurrentRelayToDiscord;

    Task ITtsHubBroadcaster.BroadcastAsync(
        TwitchUser? user,
        string message,
        CancellationToken cancellationToken
    ) => BroadcastAsync(user, message, cancellationToken);

    Task ITtsHubBroadcaster.BroadcastStateAsync(
        MARS.Server.Hubs.Models.VoiceRecognition.TtsState? state,
        CancellationToken cancellationToken
    ) => BroadcastStateAsync(state, cancellationToken);

    Task ITtsHubBroadcaster.BroadcastReassignVoiceAsync(
        string userId,
        CancellationToken cancellationToken
    ) => BroadcastReassignVoiceAsync(userId, cancellationToken);

    public async Task BroadcastAsync(
        TwitchUser? user,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (user is null || string.IsNullOrWhiteSpace(message))
        {
            logger.LogWarning("TTS broadcast was skipped because the user or message is empty.");
            return;
        }

        user = await twitchUserEnsureService.EnsureUserExistsAsync(user, cancellationToken);

        if (ttsMessageFilterService.IsFilterEnabled)
        {
            var filterResult = ttsMessageFilterService.FilterMessage(message, user.TwitchId);

            if (!filterResult)
            {
                logger.LogInformation(
                    "TTS broadcast was skipped by filter for user {User}: {Reason}",
                    user.DisplayName,
                    filterResult.Message
                );
                return;
            }

            message = filterResult.Data;
        }

        try
        {
            var sharedTtsUser = new MARS.Shared.Models.TwitchUser
            {
                TwitchId = user.TwitchId,
                UserLogin = user.UserLogin,
                DisplayName = user.AliasNickname ?? user.DisplayName,
                ProfileImageUrl = user.ProfileImageUrl,
                ChatColor = user.ChatColor,
                IsModerator = user.IsModerator,
                IsVip = user.IsVip,
                FollowedAt = user.FollowedAt,
                LastUpdated = user.LastUpdated,
                CreatedAt = user.CreatedAt,
            };

            var serverTtsUser = user.AliasNickname is not null
                ? new MARS.Server.Services.Twitch.Entitys.TwitchUser
                {
                    TwitchId = user.TwitchId,
                    UserLogin = user.UserLogin,
                    DisplayName = user.AliasNickname,
                    ProfileImageUrl = user.ProfileImageUrl,
                    ChatColor = user.ChatColor,
                    IsModerator = user.IsModerator,
                    IsVip = user.IsVip,
                    FollowedAt = user.FollowedAt,
                    LastUpdated = user.LastUpdated,
                    CreatedAt = user.CreatedAt,
                    IsInBlockList = user.IsInBlockList,
                }
                : user;

            await hubContext.Clients.Group(TtsConsumersGroupName).PlayTts(serverTtsUser, message);
            await audioHubContext
                .Clients.Group(AudioControllersGroupName)
                .PlayTts(sharedTtsUser, message);
            logger.LogInformation(
                "TTS broadcast was sent to hub consumers for user {User}",
                user.DisplayName
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast TTS message to hub consumers.");
        }
    }

    public async Task BroadcastStateAsync(
        MARS.Server.Hubs.Models.VoiceRecognition.TtsState? state,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (state is not null)
            {
                lock (_stateGate)
                {
                    CurrentVolume = Math.Clamp(state.Volume, 0.0, 2.0);
                    CurrentRelayToDiscord = state.RelayToDiscord;
                }
            }

            var serverStateToBroadcast =
                state
                ?? new MARS.Server.Hubs.Models.VoiceRecognition.TtsState
                {
                    Volume = CurrentVolume,
                    RelayToDiscord = CurrentRelayToDiscord,
                };

            var sharedStateToBroadcast = state is not null
                ? new MARS.Shared.Models.TtsState
                {
                    IsStopped = state.IsStopped,
                    Volume = state.Volume,
                    RelayToDiscord = state.RelayToDiscord,
                }
                : new MARS.Shared.Models.TtsState
                {
                    Volume = CurrentVolume,
                    RelayToDiscord = CurrentRelayToDiscord,
                };

            await hubContext
                .Clients.Group(TtsConsumersGroupName)
                .UpdateTtsState(serverStateToBroadcast);
            await audioHubContext
                .Clients.Group(AudioControllersGroupName)
                .UpdateTtsState(sharedStateToBroadcast);
            logger.LogInformation(
                "TTS state update was sent to hub consumers: {@State}",
                sharedStateToBroadcast
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast TTS state to hub consumers.");
        }
    }

    public async Task BroadcastReassignVoiceAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await hubContext.Clients.Group(TtsConsumersGroupName).ReassignVoice(userId);
            await audioHubContext.Clients.Group(AudioControllersGroupName).ReassignVoice(userId);

            logger.LogInformation(
                "Voice reassign broadcast sent to hub consumers for user {UserId}",
                userId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast voice reassign to hub consumers.");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            client.OnMessageReceived -= ClientOnOnMessageReceived;
        });

        return Task.CompletedTask;
    }

    private async Task ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        var vr = await validator
            .ForMessageReceived(args)
            .RequireChannel()
            .SkipBlacklisted()
            .ValidateWithResponseAsync(args.ChatMessage.Username);

        if (vr.IsInvalid)
        {
            return;
        }

        var messageWithoutEmotes = RemoveSevenTvEmotes(args.ChatMessage.Message);
        if (string.IsNullOrWhiteSpace(messageWithoutEmotes))
        {
            return;
        }

        await Task.Factory.StartNew(
            () =>
                BroadcastAsync(
                    TwitchUser.FromOnMessageReceivedArgs(args)!,
                    messageWithoutEmotes,
                    lifetime.ApplicationStopping
                ),
            cancellationToken: lifetime.ApplicationStopping
        );
    }

    private string RemoveSevenTvEmotes(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var words = message.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        var filteredWords = words.Where(word => !sevenTvEmoteService.IsEmote(word));

        return string.Join(' ', filteredWords).Trim();
    }
}
