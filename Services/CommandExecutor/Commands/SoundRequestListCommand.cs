using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SoundRequestListCommand(CommandsService commandsService) : BaseCommand
{
    public override string CommandName => "srlist";
    public override string Description =>
        "Добавить весь плейлист в очередь звуковых запросов (только для VIP/MOD)";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

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

        // Проверяем права VIP/MOD/Broadcaster для Twitch
        var hasPermission = true;
        if (platform == Platform.Twitch)
        {
            var isModerator =
                parameters.TryGetValue("isModerator", out var modObj) && (bool)modObj;
            var isVip = parameters.TryGetValue("isVip", out var vipObj) && (bool)vipObj;
            var isBroadcaster =
                parameters.TryGetValue("isBroadcaster", out var broadcasterObj)
                && (bool)broadcasterObj;

            hasPermission = isModerator || isVip || isBroadcaster;
        }

        if (!hasPermission)
        {
            result = "Плейлист могут заказывать только VIP/MOD";
        }
        else
        {
            var hasPlaylistUrl =
                parameters.TryGetValue("playlistUrl", out var playlistUrlObj)
                && !string.IsNullOrWhiteSpace(playlistUrlObj?.ToString());
            var hasUserId =
                parameters.TryGetValue("userId", out var userIdObj)
                && !string.IsNullOrWhiteSpace(userIdObj?.ToString());
            var hasDisplayName =
                parameters.TryGetValue("displayName", out var displayNameObj)
                && !string.IsNullOrWhiteSpace(displayNameObj?.ToString());

            if (hasPlaylistUrl && hasUserId && hasDisplayName)
            {
                var playlistUrl = playlistUrlObj!.ToString()!.Trim();
                var userId = userIdObj!.ToString()!;
                var displayName = displayNameObj!.ToString()!;

                result = await commandsService.AddPlaylistAsync(
                    playlistUrl,
                    userId,
                    displayName,
                    cancellationToken
                );
            }
            else
            {
                result = !hasPlaylistUrl
                    ? "Необходимо указать URL плейлиста"
                    : "Не удалось определить пользователя";
            }
        }

        return result;
    }
}

