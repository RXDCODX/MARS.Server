using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RollWaifuCommand(
    WaifuRollService waifoRollService,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    WaifuRollEnsurenceService waifuDbHelper
) : BaseCommand
{
    public override string CommandName => "rollwaifu";
    public override string Description => "Выполняет вайфу-ролл для пользователя";
    public override bool IsAdminCommand => true;

    public override string[] Aliases => ["waifuroll"];

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
        if (!parameters.TryGetValue(Parameters[0]!.Name, out var usernameObj))
        {
            return "Необходимо указать имя пользователя";
        }

        var username = usernameObj as string ?? "";

        username = username.StartsWith('@') ? username[1..] : username;

        try
        {
            var resultRoll = await waifoRollService.TelegramRollWaifu(username);

            if (resultRoll.Data is { Host: not null, Waifu: not null })
            {
                var waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(
                    resultRoll.Data.Waifu
                );

                var result =
                    $"Вайфу ролл для {resultRoll.Data.Host.TwitchUser?.DisplayName} выполнен!";

                await alertsHub.Clients.All.WaifuRoll(waifu, resultRoll.Data.Husband);

                return result;
            }

            return "Не удалось выполнить вайфу-ролл";
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
