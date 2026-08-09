using System.Collections.Concurrent;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Twitch.Client;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.WaifuRoll.Entitys;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.Twitch.Rewards.WaifuChat;

public class WaifuChat_TwitchReward : BackgroundService
{
    private readonly ITwitchClient _client;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly ILogger<WaifuChat_TwitchReward> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _cooldowns = new();

    private static readonly HashSet<string> IgnoredUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "nightbot",
        "streamElements",
        "moobot",
        "soundalerts",
        "commanderroot",
    };

    public WaifuChat_TwitchReward(
        ITwitchClient client,
        IDbContextFactory<AppDbContext> factory,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        ILogger<WaifuChat_TwitchReward> logger
    )
    {
        _client = client;
        _factory = factory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.OnMessageReceived += OnMessageReceived;
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _client.OnMessageReceived -= OnMessageReceived;
        await base.StopAsync(cancellationToken);
    }

    private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage.Message.Trim();

        if (
            !message.StartsWith("!жена ", StringComparison.OrdinalIgnoreCase)
            && !message.StartsWith("!waifu ", StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        var userMessage =
            message.Length > 6 ? message.Substring(message.IndexOf(' ') + 1).Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return;
        }

        var userId = e.ChatMessage.UserId;
        var displayName = e.ChatMessage.DisplayName;

        if (IgnoredUsers.Contains(displayName))
        {
            return;
        }

        if (IsOnCooldown(userId))
        {
            return;
        }

        try
        {
            await using var db = await _factory.CreateDbContextAsync();

            var husband = await db
                .Husbands.Include(h => h.TwitchUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.TwitchId == userId);

            if (husband is not { IsPrivated: true })
            {
                await _client.SendMessageToMainTwitchAsync(
                    $"@{displayName}, ты пока не женат! Сначала найди свою жену.",
                    _logger
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(husband.WaifuBrideId))
            {
                return;
            }

            var waifu = await db.Waifus.FindAsync(husband.WaifuBrideId);
            var waifuName = waifu?.Name ?? "жена";

            var correlationId = Guid.NewGuid().ToString("N");

            await _hubContext.Clients.All.WaifuChatMessage(
                correlationId,
                userId,
                displayName,
                waifuName,
                userMessage
            );

            _cooldowns[userId] = DateTime.UtcNow;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process WaifuChat for {UserId}", userId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WaifuChat command for {UserId}", userId);
        }
    }

    private bool IsOnCooldown(string userId)
    {
        if (_cooldowns.TryGetValue(userId, out var lastTime))
        {
            return (DateTime.UtcNow - lastTime).TotalSeconds < 30;
        }
        return false;
    }
}
