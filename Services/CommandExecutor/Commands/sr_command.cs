using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrCommand(SoundRequestCommandsService soundRequestCommandsService) : BaseCommand
{
    public override string CommandName => "sr";
    public override string Description => "Добавить трек в очередь звуковых запросов";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override string[] Aliases => ["soundrequest"];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "query",
                Description = "URL видео или поисковый запрос",
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
        string result;

        TwitchUser? user = null;

        if (parameters.TryGetValue("user", out var userObj))
        {
            if (userObj is TwitchUser twitchUser)
            {
                user = twitchUser;
            }
        }

        if (user == null)
        {
            result = "Не удалось получить информацию о пользователе";
        }
        else
        {
            var hasQuery =
                parameters.TryGetValue("query", out var queryObj)
                && !string.IsNullOrWhiteSpace(queryObj.ToString());

            if (hasQuery)
            {
                var query = queryObj!.ToString()!.Trim();

                result = await soundRequestCommandsService.AddTrackAsync(
                    query,
                    user,
                    cancellationToken
                );
            }
            else
            {
                result = "Необходимо указать URL видео или поисковый запрос";
            }
        }

        return result;
    }
}
