using MARS.Server.Hubs.Models.VoiceRecognition;
using MARS.Server.Services.Twitch.Entitys;
using SevenTV;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class TtsHubBroadcaster(
    IHubContext<Hubs.VoiceRecognitionHub, IVoiceRecognitionHub> hubContext,
    ILogger<TtsHubBroadcaster> logger,
    ITwitchClient client,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private const string TtsConsumersGroupName = "tts-consumers";
    private static readonly TimeSpan SevenTvEmotesCacheLifetime = TimeSpan.FromMinutes(10);
    private readonly Lock _stateGate = new();

    private readonly SevenTVClient _sevenTvClient = new();
    private readonly SemaphoreSlim _sevenTvEmotesLock = new(1, 1);
    private readonly HashSet<string> _sevenTvEmoteNames = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _sevenTvEmotesLoadedAt = DateTimeOffset.MinValue;

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

    public async Task BroadcastAsync(
        TwitchUser user,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (user is null || string.IsNullOrWhiteSpace(message))
        {
            logger.LogWarning("TTS broadcast was skipped because the user or message is empty.");
            return;
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
            && !TwitchExstension.BlackList.Any(u =>
                u.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var messageWithoutEmotes = await RemoveSevenTvEmotesAsync(
                args.ChatMessage.Message,
                lifetime.ApplicationStopping
            );
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
                    )
            );
        }
    }

    private async Task<string> RemoveSevenTvEmotesAsync(
        string message,
        CancellationToken cancellationToken
    )
    {
        var result = string.Empty;

        if (!string.IsNullOrWhiteSpace(message))
        {
            await EnsureSevenTvEmotesLoadedAsync(cancellationToken);

            if (_sevenTvEmoteNames.Count > 0)
            {
                List<string> words =
                [
                    .. message.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    ),
                ];

                List<string> filteredWords =
                [
                    .. words.Where(word => !_sevenTvEmoteNames.Contains(word)),
                ];
                result = string.Join(' ', filteredWords).Trim();
            }
            else
            {
                result = message.Trim();
            }
        }

        return result;
    }

    private async Task EnsureSevenTvEmotesLoadedAsync(CancellationToken cancellationToken)
    {
        var shouldReload =
            _sevenTvEmoteNames.Count == 0
            || DateTimeOffset.UtcNow - _sevenTvEmotesLoadedAt >= SevenTvEmotesCacheLifetime;

        if (!shouldReload)
        {
            return;
        }

        await _sevenTvEmotesLock.WaitAsync(cancellationToken);
        try
        {
            shouldReload =
                _sevenTvEmoteNames.Count == 0
                || DateTimeOffset.UtcNow - _sevenTvEmotesLoadedAt >= SevenTvEmotesCacheLifetime;
            if (!shouldReload)
            {
                return;
            }

            var sevenTvUser = await _sevenTvClient.rest.GetUser(TwitchExstension.SevenTVUserId);
            HashSet<string> emotesFromSets =
            [
                .. (sevenTvUser?.emote_sets ?? [])
                    .Where(set => set?.emotes is not null)
                    .SelectMany(set => set!.emotes!)
                    .Select(emote => emote?.name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!.Trim()),
            ];

            if (emotesFromSets.Count > 0)
            {
                _sevenTvEmoteNames.Clear();
                foreach (var emoteName in emotesFromSets)
                {
                    _sevenTvEmoteNames.Add(emoteName);
                }

                _sevenTvEmotesLoadedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                logger.LogWarning(
                    "7TV emote list is empty for user {SevenTvUserId}. TTS emote filtering is disabled.",
                    TwitchExstension.SevenTVUserId
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to load 7TV emotes. TTS message will be processed without emote filtering."
            );
        }
        finally
        {
            _sevenTvEmotesLock.Release();
        }
    }
}
