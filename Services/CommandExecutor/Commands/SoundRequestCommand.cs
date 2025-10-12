using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SoundRequestCommand(SoundRequestService soundRequestService) : BaseCommand
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

        var hasQuery =
            parameters.TryGetValue("query", out var queryObj)
            && !string.IsNullOrWhiteSpace(queryObj?.ToString());
        var hasUserId =
            parameters.TryGetValue("userId", out var userIdObj)
            && !string.IsNullOrWhiteSpace(userIdObj?.ToString());
        var hasDisplayName =
            parameters.TryGetValue("displayName", out var displayNameObj)
            && !string.IsNullOrWhiteSpace(displayNameObj?.ToString());

        if (hasQuery && hasUserId && hasDisplayName)
        {
            var query = queryObj!.ToString()!.Trim();
            var userId = userIdObj!.ToString()!;
            var displayName = displayNameObj!.ToString()!;

            result = await soundRequestService.AddTrackAsync(
                query,
                userId,
                displayName,
                cancellationToken
            );
        }
        else
        {
            result = !hasQuery
                ? "Необходимо указать URL видео или поисковый запрос"
                : "Не удалось определить пользователя";
        }

        return result;
    }
}

