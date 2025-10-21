using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;

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

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "username",
                Description = "Имя пользователя",
                Type = "string",
                Required = true,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (!parameters.TryGetValue("username", out var usernameObj))
        {
            return "Необходимо указать имя пользователя";
        }

        var username = usernameObj.ToString() ?? "";

        username = username.StartsWith('@') ? username.Substring(1) : username;

        try
        {
            var resultRoll = await waifoRollService.TelegramRollWaifu(username);

            if (resultRoll.Data is { Host: not null, Waifu: not null })
            {
                // Убеждаемся, что поля аниме и манги заполнены
                var waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(
                    resultRoll.Data.Waifu
                );

                var result =
                    $"Вайфу ролл с вайфучкой {waifu.Name} для {resultRoll.Data.Host.TwitchUser?.DisplayName} выполнен!";

                await alertsHub.Clients.All.WaifuRoll(
                    waifu,
                    resultRoll.Data.Host.TwitchUser?.DisplayName ?? throw new NullReferenceException(),
                    resultRoll.Data.Husband
                );

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
