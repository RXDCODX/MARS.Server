using System.Collections.Generic;
using System.Threading;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrlistSoundRequestListCommand(SoundRequestCommandsService soundRequestCommandsService)
    : BaseCommand
{
    public override string CommandName => "srlist";
    public override string Description =>
        "Добавить плейлист в очередь звуковых запросов с опциональным лимитом треков (только для VIP/MOD)";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override string[] Aliases => ["srlists"];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "tracksCount",
                Description =
                    "Сколько треков добавить из плейлиста. 0 или меньше - добавить максимум",
                Type = "int",
                Required = true,
                DefaultValue = "10",
            },
            new()
            {
                Name = "playlistQuery",
                Description = "URL плейлиста YouTube или SoundCloud",
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
            var hasPermission = true;
            if (platform == Platform.Twitch)
            {
                hasPermission = user.IsModerator || user.IsVip || user.IsBroadcaster;
            }

            if (!hasPermission)
            {
                result = "Плейлист могут заказывать только VIP/MOD/Broadcaster";
            }
            else
            {
                var hasPlaylistQuery = false;
                Uri? uri = null;
                string? playlistUri = null;

                if (parameters.TryGetValue("playlistQuery", out var playlistUrlObj))
                {
                    playlistUri = playlistUrlObj.ToString();

                    if (!string.IsNullOrWhiteSpace(playlistUri))
                    {
                        hasPlaylistQuery = true;
                    }
                }

                var isUrl =
                    hasPlaylistQuery && Uri.TryCreate(playlistUri, UriKind.Absolute, out uri);

                if (hasPlaylistQuery && isUrl)
                {
                    var playlistUrl = uri!.AbsoluteUri;
                    var tracksCount = 10;

                    if (parameters.TryGetValue("tracksCount", out var tracksCountObj))
                    {
                        tracksCount = Convert.ToInt32(tracksCountObj);
                    }

                    result = await soundRequestCommandsService.AddPlaylistAsync(
                        playlistUrl,
                        user,
                        tracksCount,
                        cancellationToken
                    );
                }
                else if (hasPlaylistQuery)
                {
                    var tracksCount = 10;

                    if (parameters.TryGetValue("tracksCount", out var tracksCountObj))
                    {
                        tracksCount = Convert.ToInt32(tracksCountObj);
                    }

                    result = await soundRequestCommandsService.AddPlaylistByQueryAsync(
                        playlistUri!,
                        user,
                        tracksCount,
                        cancellationToken
                    );
                }
                else
                {
                    result = "Ошибка: отсуствует запрос";
                }
            }
        }

        return result;
    }
}
