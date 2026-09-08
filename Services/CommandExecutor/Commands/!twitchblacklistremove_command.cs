using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.BlackList;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class twitchblacklistremove_command(TwitchBlackListService service) : BaseCommand
{
    public override string CommandName => "twitchblacklistremove";

    public override string Description =>
        "Команда для удаления пользователя из черного списка для пользования функциями твич алертов (и прочего)";

    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Discord, Platform.Telegram, Platform.Twitch];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Type = "string",
                Description = "@никнейм или twitchId пользователя",
                Name = "input",
                Required = true,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (parameters.TryGetValue(Parameters.First().Name, out var inputObj))
        {
            if (inputObj is string input)
            {
                var user = await service.RemoveTwitchBlacklistedUserAsync(
                    input,
                    cancellationToken: cancellationToken
                );

                if (user is not null)
                {
                    return "Юзер @" + user.DisplayName + " был успешно удален из блеклиста!";
                }

                return "Не удалось удалить юзера " + input + " из блеклист!";
            }
        }

        return "Отсуствует параметр с никнеймом";
    }
}
