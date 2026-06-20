using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrWrongCommand(SoundRequestCommandsService soundRequestCommandsService) : BaseCommand
{
    public override string CommandName => "srwrong";
    public override string Description => "Отменить последний заказанный трек или плейлист";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch, Platform.Api];

    public override CommandVisibility Visibility => CommandVisibility.All;

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
            result = await soundRequestCommandsService.CancelLastTrackAsync(
                user,
                cancellationToken
            );
        }

        return result;
    }
}
