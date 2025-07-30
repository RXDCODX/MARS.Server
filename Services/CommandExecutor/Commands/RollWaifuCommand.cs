using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.WaifuRoll;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RollWaifuCommand(
    WaifuRollService waifoRollService,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub
) : BaseCommand
{
    public override string CommandName => "rollwaifu";
    public override string Description => "Выполняет вайфу-ролл для пользователя";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override CommandParameterInfo[] Parameters => [
        new CommandParameterInfo { Name = "username", Description = "Имя пользователя", Type = "string", Required = true }
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

        try
        {
            var resultRoll = await waifoRollService.TelegramRollWaifu(username);

            if (resultRoll is { host: not null, waifu: not null })
            {
                var result =
                    $"Вайфу ролл с вайфучкой {resultRoll.waifu.Name} для {resultRoll.host.Name} выполнен!";

                await alertsHub.Clients.All.WaifuRoll(
                    resultRoll.waifu,
                    resultRoll.host.Name ?? throw new NullReferenceException(),
                    resultRoll.husband
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
