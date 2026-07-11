using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    TwitchUserEnsureService ensureService,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub
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
        displayName = displayName.StartsWith('@') ? displayName.Substring(1) : displayName;
        var color = parameters.TryGetValue("color", out var colorObj) ? colorObj?.ToString() : null;

        try
        {
            // Пользователь уже гарантирован адаптером (TwitchCommandService)
            TwitchUser? twitchUser = parameters.TryGetValue("user", out var userObj)
                ? userObj as TwitchUser
                : null;

            // Fallback для платформ без адаптера (Telegram, API)
            twitchUser ??= await ensureService.EnsureUserExistsByLoginAsync(
                displayName,
                cancellationToken
            );

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
                await alertsHub.Clients.All.FumoRoll(fumo, twitchUser, color);
            }

            return twitchUser.DisplayName;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
