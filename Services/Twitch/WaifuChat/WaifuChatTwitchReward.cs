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

        if (
            message.StartsWith("!жена ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("!waifu ", StringComparison.OrdinalIgnoreCase)
        )
        {
            var userMessage = message[(message.IndexOf(' ') + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(userMessage))
            {
                var vr = await validator
                    .ForMessageReceived(e)
                    .RequireBroadcasterId()
                    .SkipBlacklisted()
                    .RequireUserId()
                    .ValidateWithResponseAsync(e.ChatMessage.Username);

                if (!vr.IsInvalid)
                {
                    var userId = e.ChatMessage.UserId;
                    var displayName = e.ChatMessage.DisplayName;

                    if (!IgnoredUsers.Contains(displayName) && !IsOnCooldown(userId))
                    {
                        try
                        {
                            await using var db = await factory.CreateDbContextAsync();

                            var husband = await db
                                .Husbands.Include(h => h.TwitchUser)
                                .Include(h => h.HusbandGreetings)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(h => h.TwitchId == userId);

                            if (husband is { IsPrivated: true })
                            {
                                if (!string.IsNullOrWhiteSpace(husband.WaifuBrideId))
                                {
                                    var waifu = await db.Waifus.FindAsync(husband.WaifuBrideId);
                                    var waifuName = waifu?.Name ?? "жена";

                                    var characterDescription = await GetCharacterDescriptionAsync(
                                        waifu?.ShikiId
                                    );

                                    // Проверяем было ли приветствие сегодня
                                    var lastGreeting = husband.HusbandGreetings?.Time;
                                    var wasGreetedToday = lastGreeting.HasValue
                                        && (DateTime.UtcNow - lastGreeting.Value).TotalHours < 20;

                                    var correlationId = Guid.NewGuid().ToString("N");

                                    await hubContext.Clients.All.WaifuChatMessage(
                                        new WaifuChatMessage
                                        {
                                            CorrelationId = correlationId,
                                            TwitchId = userId,
                                            DisplayName = displayName,
                                            WaifuName = waifuName,
                                            Message = userMessage,
                                            MessageId = e.ChatMessage.Id,
                                            CharacterDescription = characterDescription,
                                            LastAutoHelloMessage = wasGreetedToday
                                                ? $"Ты уже приветствовала мужа сегодня в {lastGreeting:HH:mm}."
                                                : null,
                                        }
                                    );

                                    _cooldowns[userId] = DateTime.UtcNow;
                                }
                            }
                            else
                            {
                                await client.SendMessageToMainTwitchAsync(
                                    $"@{displayName}, ты пока не женат! Сначала найди свою жену.",
                                    logger
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                ex,
                                "Error processing WaifuChat command for {UserId}",
                                userId
                            );
                        }
                    }
                }
            }
        }
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
