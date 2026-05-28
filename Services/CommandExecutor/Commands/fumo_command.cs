using System.Collections.Generic;
using System.Threading;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class FumoCommand(IHubContext<TelegramusHub, ITelegramusHub> alertsHub) : BaseCommand
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
        displayName = displayName.StartsWith('@') ? displayName.Substring(1) : displayName;
        var color = parameters.TryGetValue("color", out var colorObj) ? colorObj?.ToString() : null;

        if (!string.IsNullOrWhiteSpace(color))
        {
            await alertsHub.Clients.All.FumoFriday(displayName, color);
            return $"Фумо фрайдей с {displayName} с цветом {color} объявлен!";
        }

        await alertsHub.Clients.All.FumoFriday(displayName);
        return $"Фумо фрайдей с {displayName} объявлен!";
    }
}
