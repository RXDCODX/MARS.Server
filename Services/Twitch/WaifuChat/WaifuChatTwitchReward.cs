using System.Collections.Concurrent;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Twitch.Validation;
using MARS.Shared.Models.WaifuChat;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using AudioHub = MARS.Server.Hubs.AudioControllerHub;

namespace MARS.Server.Services.Twitch.WaifuChat;

public class WaifuChatTwitchReward(
    ITwitchClient client,
    IDbContextFactory<AppDbContext> factory,
    IHubContext<AudioHub.AudioControllerHub, MARS.Shared.Hubs.IAudioControllerHub> hubContext,
    ILogger<WaifuChatTwitchReward> logger,
    ITwitchEventValidationService validator,
    ShikimoriService shikimoriService
) : BackgroundService
{
    private readonly ConcurrentDictionary<string, DateTime> _cooldowns = new();
    private readonly ConcurrentDictionary<string, string> _characterDescriptionCache = new();

    private static readonly HashSet<string> IgnoredUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "nightbot",
        "streamelements",
        "moobot",
        "soundalerts",
        "commanderroot",
    };

    private static readonly string[] TriggerKeywords =
    [
        "жена", "wife", "муж", "husband", "супруг", "spouse",
        "партнёр", "partner", "половинка", "милая", "милый",
        "дорогая", "дорогой", "любимая", "любимый",
        "котик", "солнце", "зайка",
    ];

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        client.OnMessageReceived += OnMessageReceived;
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= OnMessageReceived;
        await base.StopAsync(cancellationToken);
    }

    private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage.Message.Trim();

        if (!MightBeWaifuChat(message))
        {
            return;
        }

        var vr = await validator
            .ForMessageReceived(e)
            .RequireBroadcasterId()
            .SkipBlacklisted()
            .RequireUserId()
            .ValidateWithResponseAsync(e.ChatMessage.Username);

        if (vr.IsInvalid)
        {
            return;
        }

        var userId = e.ChatMessage.UserId;
        var displayName = e.ChatMessage.DisplayName;

        if (IgnoredUsers.Contains(displayName) || IsOnCooldown(userId))
        {
            return;
        }

        try
        {
            await using var db = await factory.CreateDbContextAsync();

            var husband = await db
                .Husbands.Include(h => h.TwitchUser)
                .Include(h => h.HusbandGreetings)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.TwitchId == userId);

            if (husband is not { IsPrivated: true })
            {
                await client.SendMessageToMainTwitchAsync(
                    $"@{displayName}, ты пока не женат! Сначала найди свою жену.",
                    logger
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(husband.WaifuBrideId))
            {
                return;
            }

            var waifu = await db.Waifus.FindAsync(husband.WaifuBrideId);
            var waifuName = waifu?.Name ?? "жена";

            var characterDescription = await GetCharacterDescriptionAsync(waifu?.ShikiId);

            var lastGreeting = husband.HusbandGreetings?.Time;
            var wasGreetedToday =
                lastGreeting.HasValue && (DateTime.UtcNow - lastGreeting.Value).TotalHours < 20;

            var correlationId = Guid.NewGuid().ToString("N");

            await hubContext.Clients.All.WaifuChatMessage(
                new WaifuChatMessage
                {
                    CorrelationId = correlationId,
                    TwitchId = userId,
                    DisplayName = displayName,
                    WaifuName = waifuName,
                    Message = message,
                    MessageId = e.ChatMessage.Id,
                    CharacterDescription = characterDescription,
                    LastAutoHelloMessage = wasGreetedToday
                        ? $"Ты уже приветствовала мужа сегодня в {lastGreeting:HH:mm}."
                        : null,
                }
            );

            _cooldowns[userId] = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing WaifuChat command for {UserId}", userId);
        }
    }

    public bool MightBeWaifuChat(string message)
    {
        if (
            message.StartsWith("!жена ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("!waifu ", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        var lower = message.ToLowerInvariant();
        return TriggerKeywords.Any(kw => lower.Contains(kw));
    }

    private async Task<string?> GetCharacterDescriptionAsync(string? shikiId)
    {
        if (string.IsNullOrWhiteSpace(shikiId))
        {
            return null;
        }

        if (_characterDescriptionCache.TryGetValue(shikiId, out var cached))
        {
            return cached;
        }

        if (long.TryParse(shikiId, out var id))
        {
            var character = await shikimoriService.GetShikiCharacterById(id);
            if (character?.Description is { Length: > 0 } description)
            {
                _characterDescriptionCache[shikiId] = description;
                return description;
            }
        }

        return null;
    }

    private bool IsOnCooldown(string userId)
    {
        if (_cooldowns.TryGetValue(userId, out var lastTime))
        {
            return (DateTime.UtcNow - lastTime).TotalSeconds < 10;
        }
        return false;
    }
}
