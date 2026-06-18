using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Hubs.Models.VoiceRecognition;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchUser = MARS.Server.Services.Twitch.Entitys.TwitchUser;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class TtsHubBroadcaster(
    IHubContext<Hubs.VoiceRecognitionHub, IVoiceRecognitionHub> hubContext,
    ILogger<TtsHubBroadcaster> logger,
    ITwitchClient client,
    IHostApplicationLifetime lifetime,
    ISevenTvEmoteService sevenTvEmoteService,
    ITtsMessageFilterService ttsMessageFilterService
) : BackgroundService, ITtsHubBroadcaster
{
    private const string TtsConsumersGroupName = "tts-consumers";
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

    double ITtsHubBroadcaster.CurrentVolume => CurrentVolume;

    Task ITtsHubBroadcaster.BroadcastAsync(
        TwitchUser? user,
        string message,
        CancellationToken cancellationToken
    ) => BroadcastAsync(user, message, cancellationToken);

    Task ITtsHubBroadcaster.BroadcastStateAsync(
        TtsState? state,
        CancellationToken cancellationToken
    ) => BroadcastStateAsync(state, cancellationToken);

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

        if (ttsMessageFilterService.IsFilterEnabled)
        {
            var filterResult = ttsMessageFilterService.FilterMessage(
                message,
                user.TwitchId
            );

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
            await hubContext.Clients.Group(TtsConsumersGroupName).PlayTts(user, message);
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
        TtsState? state,
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
                }
            }

            var stateToBroadcast = state ?? new TtsState { Volume = CurrentVolume };

            await hubContext.Clients.Group(TtsConsumersGroupName).UpdateTtsState(stateToBroadcast);
            logger.LogInformation(
                "TTS state update was sent to hub consumers: {@State}",
                stateToBroadcast
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast TTS state to hub consumers.");
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
        if (
            args.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Logins.Any(u =>
                u.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
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
