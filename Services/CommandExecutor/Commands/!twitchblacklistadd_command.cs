using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.BlackList;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class twitchblacklistadd_command(TwitchBlackListService service) : BaseCommand
{
    public override string CommandName => "twitchblacklistadd";

    public override string Description =>
        "Команда для добавления пользователя в черный список для пользования функциями твич алертов (и прочего)";

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
                var user = await service.AddTwitchBlacklistedUserAsync(
                    input,
                    cancellationToken: cancellationToken
                );

                if (user is not null)
                {
                    return "Юзер @" + user.DisplayName + " был успешно добавлен в блеклист!";
                }

                return "Не удалось добавить юзера " + input + " в блеклист!";
            }
        }

        return "Отсуствует параметр с никнеймом";
    }
}
