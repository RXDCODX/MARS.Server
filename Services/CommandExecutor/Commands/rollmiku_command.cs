using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards._4_MikuRoll;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RollMikuCommand(
    MikuRollService mikuRollService,
    MikuCollectionService collectionService,
    TwitchUserEnsureService ensureService,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    ILogger<RollMikuCommand> logger
) : BaseCommand
{
    public override string CommandName => "rollmiku";
    public override string Description => "Выполняет мику-ролл";
    public override bool IsAdminCommand => true;
    public override string[] Aliases => ["mikuroll"];

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "displayName",
                Description = "Имя пользователя",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "color",
                Description = "Цвет (опционально)",
                Type = "string",
                Required = false,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (!parameters.TryGetValue("displayName", out var displayNameObj))
        {
            return "Необходимо указать имя пользователя";
        }

        var displayName = displayNameObj.ToString() ?? "";
        displayName = displayName.StartsWith('@') ? displayName[1..] : displayName;
        var color = parameters.TryGetValue("color", out var colorObj) ? colorObj?.ToString() : null;

        try
        {
            // Пользователь уже гарантирован адаптером (TwitchCommandService)

            TwitchUser? twitchUser =
                // Fallback для платформ без адаптера (Telegram, API)
                await ensureService.EnsureUserExistsByLoginAsync(displayName, cancellationToken);

            if (twitchUser is null)
            {
                return $"Пользователь {displayName} не найден";
            }

            if (!string.IsNullOrWhiteSpace(color))
            {
                twitchUser.ChatColor = color;
            }

            var module = await mikuRollService.GetNextMikuModuleAsync(cancellationToken);

            if (module is not null)
            {
                var collectedCount = 0;
                var totalCount = 0;

                try
                {
                    var (collected, total) = await collectionService.GetUserCollectionStatsAsync(
                        twitchUser.TwitchId
                    );
                    collectedCount = collected;
                    totalCount = total;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to get Miku collection stats for {UserId}",
                        twitchUser.TwitchId
                    );
                }

                await alertsHub.Clients.All.MikuRoll(
                    module,
                    twitchUser,
                    collectedCount,
                    totalCount
                );
            }

            return $"Мику ролл для {twitchUser?.DisplayName} выполнен!";
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
