using System.Collections.Generic;
using System.Threading;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrlistSoundRequestListCommand(CommandsService commandsService) : BaseCommand
{
    public override string CommandName => "srlist";
    public override string Description =>
        "Добавить плейлист в очередь звуковых запросов с опциональным лимитом треков (только для VIP/MOD)";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override string[] Aliases => ["srlists"];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "playlistUrl",
                Description = "URL плейлиста YouTube или SoundCloud",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "tracksCount",
                Description = "Сколько треков добавить из плейлиста. 0 или меньше - добавить максимум",
                Type = "int",
                Required = false,
                DefaultValue = "10",
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
            var hasPermission = true;
            if (platform == Platform.Twitch)
            {
                hasPermission =
                    user.IsModerator
                    || user.IsVip
                    || user.IsBroadcaster;
            }

            if (!hasPermission)
            {
                result = "Плейлист могут заказывать только VIP/MOD/Broadcaster";
            }
            else
            {
                var hasPlaylistUrl =
                    parameters.TryGetValue("playlistUrl", out var playlistUrlObj)
                    && !string.IsNullOrWhiteSpace(playlistUrlObj?.ToString());

                if (hasPlaylistUrl)
                {
                    var playlistUrl = playlistUrlObj!.ToString()!.Trim();
                    var tracksCount = 10;

                    if (parameters.TryGetValue("tracksCount", out var tracksCountObj))
                    {
                        tracksCount = Convert.ToInt32(tracksCountObj);
                    }

                    result = await commandsService.AddPlaylistAsync(
                        playlistUrl,
                        user,
                        tracksCount,
                        cancellationToken
                    );
                }
                else
                {
                    result = "Необходимо указать URL плейлиста";
                }
            }
        }

        return result;
    }
}
