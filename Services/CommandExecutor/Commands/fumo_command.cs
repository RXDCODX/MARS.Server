using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class FumoCommand(
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    TwitchUserEnsureService ensureService
) : BaseCommand
{
    public override string CommandName => "fumo";
    public override string Description => "Отправляет фумо в чат";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

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

        var twitchUser = await ensureService.EnsureUserExistsByLoginAsync(
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

        await alertsHub.Clients.All.FumoFriday(twitchUser);
        return $"Фумо фрайдей с {displayName} объявлен!";
    }
}
