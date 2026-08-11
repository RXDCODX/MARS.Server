using System.Collections.Concurrent;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Twitch.Validation;
using MARS.Server.Services.WaifuRoll.Entitys;
using MARS.Shared.Models.WaifuChat;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;
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
        "жена",
        "wife",
        "муж",
        "husband",
        "супруг",
        "spouse",
        "партнёр",
        "partner",
        "половинка",
        "милая",
        "милый",
        "дорогая",
        "дорогой",
        "любимая",
        "любимый",
        "котик",
        "солнце",
        "зайка",
    ];

    private static readonly string[] AutoHelloPatterns =
    [
        "прислал",
        "поздравляет",
        "сообщение:",
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
        var displayName = e.ChatMessage.DisplayName;
        var userId = e.ChatMessage.UserId;

        logger.LogDebug(
            "[WaifuChat] Received: '{Message}' from {DisplayName} ({UserId})",
            message,
            displayName,
            userId
        );

        if (!MightBeWaifuChat(message, e.ChatMessage))
        {
            logger.LogDebug(
                "[WaifuChat] Skipping '{Message}' — не похоже на обращение к жене",
                message
            );
            return;
        }

        logger.LogInformation(
            "[WaifuChat] Message '{Message}' от {DisplayName} ({UserId}) прошёл keyword check",
            message,
            displayName,
            userId
        );

        var vr = await validator
            .ForMessageReceived(e)
            .RequireBroadcasterId()
            .SkipBlacklisted()
            .RequireUserId()
            .ValidateWithResponseAsync(e.ChatMessage.Username);

        if (vr.IsInvalid)
        {
            logger.LogWarning(
                "[WaifuChat] Validation failed for {DisplayName}: {Error}",
                displayName,
                vr.FirstError
            );
            return;
        }

        if (IgnoredUsers.Contains(displayName))
        {
            logger.LogDebug("[WaifuChat] Skipping ignored user {DisplayName}", displayName);
            return;
        }

        if (IsOnCooldown(userId))
        {
            logger.LogDebug("[WaifuChat] Skipping {DisplayName} — cooldown", displayName);
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
                logger.LogDebug(
                    "[WaifuChat] {DisplayName} не женат (IsPrivated={IsPrivated})",
                    displayName,
                    husband?.IsPrivated
                );
                await client.SendMessageToMainTwitchAsync(
                    $"@{displayName}, ты пока не женат! Сначала найди свою жену.",
                    logger
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(husband.WaifuBrideId))
            {
                logger.LogWarning(
                    "[WaifuChat] {DisplayName} женат, но WaifuBrideId пустой",
                    displayName
                );
                return;
            }

            var waifu = await db.Waifus.FindAsync(husband.WaifuBrideId);
            var waifuName = waifu?.Name ?? "жена";

            var characterDescription = await GetCharacterDescriptionAsync(waifu?.ShikiId);

            // Определяем контекст AutoHello
            var autoHelloContext = await GetAutoHelloContextAsync(
                e.ChatMessage, userId, husband, db);

            var correlationId = Guid.NewGuid().ToString("N");

            logger.LogInformation(
                "[WaifuChat] Sending to AudioController: correlationId={CorrelationId}, "
                    + "userId={UserId}, displayName={DisplayName}, waifuName={WaifuName}, "
                    + "messageId={MessageId}, hasCharDescr={HasDescr}, autoHelloContext={HasContext}",
                correlationId,
                userId,
                displayName,
                waifuName,
                e.ChatMessage.Id,
                characterDescription?.Length > 0,
                autoHelloContext?.Length > 0
            );

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
                    LastAutoHelloMessage = autoHelloContext,
                }
            );

            _cooldowns[userId] = DateTime.UtcNow;

            logger.LogInformation(
                "[WaifuChat] Message sent to AudioController successfully for {DisplayName}",
                displayName
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[WaifuChat] Error processing message for {DisplayName} ({UserId})",
                displayName,
                userId
            );
        }
    }

    public bool MightBeWaifuChat(string message, ChatMessage? chatMessage = null)
    {
        // Команда !жена / !waifu
        if (
            message.StartsWith("!жена ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("!waifu ", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        // Keyword check
        var lower = message.ToLowerInvariant();
        if (TriggerKeywords.Any(kw => lower.Contains(kw)))
        {
            return true;
        }

        // Реплай на AutoHello сообщение бота
        if (chatMessage is not null && IsReplyToBotAutoHello(chatMessage))
        {
            return true;
        }

        return false;
    }

    private static bool IsReplyToBotAutoHello(ChatMessage chatMessage)
    {
        var reply = chatMessage.ChatReply;
        if (reply is null)
        {
            return false;
        }

        // Проверяем что реплай на сообщение бота
        if (
            !string.Equals(
                reply.ParentUserLogin,
                TwitchExstension.BotName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        // Проверяем что текст содержит AutoHello паттерн
        var parentBody = reply.ParentMsgBody ?? "";
        return AutoHelloPatterns.Any(pattern =>
            parentBody.Contains(pattern, StringComparison.OrdinalIgnoreCase)
        );
    }

    private async Task<string?> GetAutoHelloContextAsync(
        ChatMessage chatMessage,
        string userId,
        Husband husband,
        AppDbContext db)
    {
        // Если это реплай на AutoHello — передаём текст приветствия
        if (chatMessage.ChatReply is { } reply && IsReplyToBotAutoHello(chatMessage))
        {
            var parentBody = reply.ParentMsgBody ?? "";
            logger.LogInformation(
                "[WaifuChat] Reply to AutoHello detected: {ParentBody}",
                parentBody
            );
            return $"Муж ответил на твоё приветственное сообщение: \"{parentBody}\"";
        }

        // Иначе — проверяем было ли приветствие сегодня
        var lastGreeting = husband.HusbandGreetings?.Time;
        var wasGreetedToday =
            lastGreeting.HasValue && (DateTime.UtcNow - lastGreeting.Value).TotalHours < 20;

        if (wasGreetedToday)
        {
            return $"Ты уже приветствовала мужа сегодня в {lastGreeting:HH:mm}.";
        }

        return null;
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
