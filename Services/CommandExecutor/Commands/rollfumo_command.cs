using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards._4_FumoRoll;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RollFumoCommand(
    FumoRollService fumoRollService,
    FumoCollectionService collectionService,
    TwitchUserEnsureService ensureService,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    ILogger<RollFumoCommand> logger
) : BaseCommand
{
    public override string CommandName => "rollfumo";
    public override string Description => "Выполняет фумо-ролл";
    public override bool IsAdminCommand => true;
    public override string[] Aliases => ["fumoroll"];

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

            var fumo = await fumoRollService.RollTheFumo();

            if (fumo is not null)
            {
                var collectedCount = 0;
                var totalCount = 0;
                try
                {
                    var stats = await collectionService.RecordRollAsync(
                        twitchUser.TwitchId,
                        fumo.MfcId
                    );
                    collectedCount = stats.CollectedCount;
                    totalCount = stats.TotalCount;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to record Fumo collection for {UserId}",
                        twitchUser.TwitchId
                    );
                }

                await alertsHub.Clients.All.FumoRoll(fumo, twitchUser, collectedCount, totalCount);
            }

            return $"Фумо ролл для {twitchUser?.DisplayName} выполнен!";
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
