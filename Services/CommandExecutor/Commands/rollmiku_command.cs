using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards._4_MikuModuleRoll;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RollMikuCommand(
    MikuModuleRollService mikuRollService,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub
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
        displayName = displayName.StartsWith('@') ? displayName.Substring(1) : displayName;
        var color = parameters.TryGetValue("color", out var colorObj) ? colorObj?.ToString() : null;

        try
        {
            var module = await mikuRollService.RollTheMikuModule();

            if (module is not null)
            {
                var twitchUser = new TwitchUser
                {
                    TwitchId = "",
                    UserLogin = displayName.ToLowerInvariant(),
                    DisplayName = displayName,
                    ProfileImageUrl = "",
                    ChatColor = color,
                };

                await alertsHub.Clients.All.MikuRoll(module, twitchUser, color);

                return $"Мику-ролл: {module.JapaneseName ?? module.Title} для {displayName}";
            }

            return "Не удалось выполнить мику-ролл";
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
