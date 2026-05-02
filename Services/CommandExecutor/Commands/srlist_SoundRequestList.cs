using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrlistSoundRequestListCommand(CommandsService commandsService) : BaseCommand
{
    public override string CommandName => "srlist";
    public override string Description =>
        "Добавить весь плейлист в очередь звуковых запросов (только для VIP/MOD)";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override string[] Aliases => ["srlists"];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "playlistUrl",
                Description = "URL плейлиста YouTube",
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
                hasPermission =
                    user.IsModerator
                    || user.IsVip
                    || user.TwitchId.Equals(
                        TwitchExstension.ChannelId,
                        StringComparison.OrdinalIgnoreCase
                    );
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

                    result = await commandsService.AddPlaylistAsync(
                        playlistUrl,
                        user,
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
