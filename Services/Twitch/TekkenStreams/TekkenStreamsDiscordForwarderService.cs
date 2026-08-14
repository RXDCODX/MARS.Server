using System.Collections.Concurrent;
using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Discord.Gateway;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Api.Helix.Models.Chat.ChatSettings;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace MARS.Server.Services.Twitch.TekkenStreams;

/// <summary>
/// Сервис для подключения к чатам русскоязычных теккен-стримов и пересылки
/// их сообщений в Discord канал, заданный через RootState.TekkenStreamsDiscordChannelId.
/// </summary>
public class TekkenStreamsDiscordForwarderService(
    ITwitchClient twitchClient,
    ITwitchAPI api,
    IDiscordGatewayService discordGatewayService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<TekkenStreamsDiscordForwarderService> logger
) : BackgroundService
{
    private const string TekkenGameId = "538054672";
    private const string StreamLanguage = "ru";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, byte> _tekkenChannels = new(
        StringComparer.OrdinalIgnoreCase
    );

    private ulong _discordChannelId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        twitchClient.OnMessageReceived += OnMessageReceived;

        using var timer = new PeriodicTimer(RefreshInterval);

        await SyncStreamsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncStreamsAsync(stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        twitchClient.OnMessageReceived -= OnMessageReceived;
        var result = base.StopAsync(cancellationToken);
        return result;
    }

    public async Task SyncStreamsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _discordChannelId = await GetDiscordChannelIdAsync(cancellationToken);
            var streams = await GetRuTekkenStreamsAsync(cancellationToken);
            var streamLogins = streams
                .Select(e => e.UserLogin)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            await LeaveStoppedStreamsAsync(streamLogins);
            await JoinNewStreamsAsync(streams, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка синхронизации каналов теккен-стримов");
        }
    }

    public async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var channel = e.ChatMessage.Channel;

        if (
            channel.Equals(TwitchExstension.Channel, StringComparison.OrdinalIgnoreCase)
            || !_tekkenChannels.ContainsKey(channel)
            || _discordChannelId == 0
        )
        {
            return;
        }

        var message = $"[{channel}] {e.ChatMessage.DisplayName}: {e.ChatMessage.Message}";
        var sendResult = await discordGatewayService.SendMessageAsync(
            _discordChannelId,
            message,
            CancellationToken.None
        );

        if (sendResult.Success)
        {
            logger.LogDebug("Переслано сообщение из {Channel} в Discord", channel);
        }
        else
        {
            logger.LogWarning(
                "Не удалось переслать сообщение из {Channel} в Discord: {Error}",
                channel,
                sendResult.Message
            );
        }
    }

    private async Task LeaveStoppedStreamsAsync(HashSet<string> streamLogins)
    {
        foreach (var joinedChannel in twitchClient.JoinedChannels)
        {
            if (
                joinedChannel.Channel.Equals(
                    TwitchExstension.Channel,
                    StringComparison.OrdinalIgnoreCase
                ) || streamLogins.Contains(joinedChannel.Channel)
            )
            {
                continue;
            }

            await twitchClient.LeaveChannelAsync(joinedChannel);
            _tekkenChannels.TryRemove(joinedChannel.Channel, out _);
            logger.LogInformation(
                "Выход из чата канала {Channel}, стрим завершён",
                joinedChannel.Channel
            );
        }
    }

    private async Task JoinNewStreamsAsync(Stream[] streams, CancellationToken cancellationToken)
    {
        foreach (var stream in streams)
        {
            if (
                stream.UserLogin.Equals(
                    TwitchExstension.Channel,
                    StringComparison.OrdinalIgnoreCase
                )
                || twitchClient.JoinedChannels.Any(e =>
                    e.Channel.Equals(stream.UserLogin, StringComparison.OrdinalIgnoreCase)
                )
                || !await IsChatJoinableAsync(stream.Id, cancellationToken)
            )
            {
                continue;
            }

            await twitchClient.JoinChannelAsync(stream.UserLogin);
            _tekkenChannels.TryAdd(stream.UserLogin, 0);
            logger.LogInformation("Подключение к чату канала {Channel}", stream.UserLogin);

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    private async Task<bool> IsChatJoinableAsync(
        string broadcasterId,
        CancellationToken cancellationToken
    )
    {
        var result = true;

        try
        {
            var response = await api.Helix.Chat.GetChatSettingsAsync(broadcasterId, broadcasterId);
            var settings = response.Data[0];

            if (
                settings is not null
                && !settings.FollowerMode
                && !settings.EmoteMode
                && !settings.SubscriberMode
                && !settings.UniqueChatMode
            )
            {
                logger.LogDebug("Чат канала {BroadcasterId} доступен для пересылки", broadcasterId);
            }
            else
            {
                result = false;
                logger.LogInformation(
                    "Чат канала {BroadcasterId} имеет ограничения, пропускаем",
                    broadcasterId
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Не удалось получить настройки чата {BroadcasterId}, считаем канал доступным",
                broadcasterId
            );
        }

        return result;
    }

    private async Task<Stream[]> GetRuTekkenStreamsAsync(CancellationToken cancellationToken)
    {
        Stream[] result = [];

        try
        {
            var response = await api.Helix.Streams.GetStreamsAsync(
                first: 100,
                gameIds: [TekkenGameId],
                languages: [StreamLanguage]
            );
            result = response.Streams;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения списка теккен-стримов");
        }

        return result;
    }

    private async Task<ulong> GetDiscordChannelIdAsync(CancellationToken cancellationToken)
    {
        var result = 0UL;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var state = await dbContext
                .RootState.AsNoTracking()
                .SingleOrDefaultAsync(
                    e => e.Name == RootStateKeys.TekkenStreamsDiscordChannelId,
                    cancellationToken
                );

            if (state != null && ulong.TryParse(state.Value, out var channelId))
            {
                result = channelId;
            }
            else
            {
                logger.LogDebug(
                    "Ключ RootState '{Key}' не задан или имеет некорректное значение",
                    RootStateKeys.TekkenStreamsDiscordChannelId
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка чтения ключа RootState '{Key}'",
                RootStateKeys.TekkenStreamsDiscordChannelId
            );
        }

        return result;
    }
}
