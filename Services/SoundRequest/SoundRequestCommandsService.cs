using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.SoundRequest.YouTube;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.SoundRequest;

public class SoundRequestCommandsService(
    ITwitchClient client,
    IHostApplicationLifetime lifetime,
    IDbContextFactory<AppDbContext> dbFactory,
    YouTubeResolver ytResolver,
    SoundRequestUserQueue queue
) : BackgroundService
{
    private readonly CancellationToken _token = lifetime.ApplicationStopping;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += OnMessageReceived;
        });

        return Task.CompletedTask;
    }

    private async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (
            !e.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        var message = e.ChatMessage.Message.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var isSr =
            message.StartsWith("!sr", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("!soundrequest", StringComparison.OrdinalIgnoreCase);

        if (isSr)
        {
            var cmd = isSr ? "!sr" : "!soundrequest";
            var arg = message.Length > cmd.Length ? message[cmd.Length..].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(arg))
            {
                return;
            }

            BaseTrackInfo? info = null;
            if (Uri.TryCreate(arg, UriKind.Absolute, out _))
            {
                info = await ytResolver.ResolveVideoAsync(arg, _token);
            }
            else
            {
                // текстовый запрос — ищем через YouTube Music API
                info = await ytResolver.ResolveQueryAsync(arg, _token);
            }

            if (info == null)
            {
                await client.SendMessageToMainTwitchAsync("Не удалось распознать видео по ссылке");
                return;
            }

            await using var db = await dbFactory.CreateDbContextAsync(_token);
            var exists = await db
                .SoundRequestBaseTrackInfos.AsNoTracking()
                .AnyAsync(t => t.Url == info.Url, _token);
            if (!exists)
            {
                db.SoundRequestBaseTrackInfos.Add(info);
                await db.SaveChangesAsync(_token);
            }

            await queue.AddToQueueAsync(
                new UserRequestedTrack
                {
                    RequestedTrack = info,
                    RequestedTrackId = info.Id,
                    TwitchId = e.ChatMessage.UserId,
                    TwitchDisplayName = e.ChatMessage.DisplayName,
                }
            );

            var duration = info.Duration;
            var durationText =
                duration > TimeSpan.Zero
                    ? $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}"
                    : "??:??";
            await client.SendMessageToMainTwitchAsync($"Добавлено: {info.Title} [{durationText}]");
            return;
        }

        if (message.Equals("!song", StringComparison.OrdinalIgnoreCase))
        {
            await using var db = await dbFactory.CreateDbContextAsync(_token);
            var last = await db
                .SoundRequestBaseTrackInfos.AsNoTracking()
                .OrderByDescending(t => t.LastTimePlays)
                .FirstOrDefaultAsync(_token);
            if (last != null)
            {
                await client.SendMessageToMainTwitchAsync($"Сейчас: {last.Title}");
            }
            return;
        }

        if (message.Equals("!queue", StringComparison.OrdinalIgnoreCase))
        {
            var list = await queue.GetQueueAsync();
            var idx = list.FindIndex(t => t.TwitchId == e.ChatMessage.UserId);
            if (idx >= 0)
            {
                await client.SendMessageToMainTwitchAsync($"Ваша позиция в очереди: {idx + 1}");
            }
            else
            {
                await client.SendMessageToMainTwitchAsync("Вы не в очереди");
            }
            return;
        }

        if (message.Equals("!wrong", StringComparison.OrdinalIgnoreCase))
        {
            await using var db = await dbFactory.CreateDbContextAsync(_token);
            var last = await db
                .SoundRequestUserQueue.Where(t => t.TwitchId == e.ChatMessage.UserId)
                .OrderByDescending(t => t.Order)
                .FirstOrDefaultAsync(_token);
            if (last == null)
            {
                await client.SendMessageToMainTwitchAsync("Нечего отменять");
                return;
            }

            db.SoundRequestUserQueue.Remove(last);
            await db.SaveChangesAsync(_token);
            await client.SendMessageToMainTwitchAsync("Последний заказ удален");
            return;
        }

        if (message.StartsWith("!srlist ", StringComparison.OrdinalIgnoreCase))
        {
            var link = message[8..].Trim();
            var isVipOrMod =
                e.ChatMessage.IsModerator
                || e.ChatMessage.IsMe
                || e.ChatMessage.IsVip
                || e.ChatMessage.IsBroadcaster;
            if (!isVipOrMod)
            {
                await client.SendMessageToMainTwitchAsync(
                    "Плейлист могут заказывать только VIP/MOD"
                );
                return;
            }

            var items = await ytResolver.ResolvePlaylistAsync(link) ?? [];
            if (items.Length == 0)
            {
                await client.SendMessageToMainTwitchAsync("Не удалось прочитать плейлист");
                return;
            }

            await using var db = await dbFactory.CreateDbContextAsync(_token);
            foreach (var info in items)
            {
                var exists = await db
                    .SoundRequestBaseTrackInfos.AsNoTracking()
                    .AnyAsync(t => t.Url == info.Url, _token);
                if (!exists)
                {
                    db.SoundRequestBaseTrackInfos.Add(info);
                    await db.SaveChangesAsync(_token);
                }

                await queue.AddToQueueAsync(
                    new UserRequestedTrack
                    {
                        RequestedTrack = info,
                        RequestedTrackId = info.Id,
                        TwitchId = e.ChatMessage.UserId,
                        TwitchDisplayName = e.ChatMessage.DisplayName,
                    }
                );
            }

            await client.SendMessageToMainTwitchAsync($"Добавлено треков: {items.Length}");
        }
    }
}
