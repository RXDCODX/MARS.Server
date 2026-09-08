using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards._4_MikuRoll;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class MikuInventoryCommand(
    MikuCollectionService collectionService,
    ITwitchUserEnsureService ensureService
) : BaseCommand
{
    private const int MaxListLength = 450;

    public override string CommandName => "mikuinv";
    public override string Description => "Показывает инвентарь мику-модулей";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch, Platform.Telegram, Platform.Api];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "displayName",
                Description = "Имя пользователя (опционально)",
                Type = "string",
                Required = false,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось определить пользователя.";

        var (twitchUser, requestedLogin) = await ResolveTargetUserAsync(
            parameters,
            cancellationToken
        );

        if (twitchUser is not null)
        {
            var (collected, total, items) = await collectionService.GetInventoryAsync(
                twitchUser.TwitchId,
                cancellationToken
            );

            var listPart = BuildListPart(items);

            result = $"У {twitchUser.DisplayName} собрано {collected}/{total} модулей";
            if (listPart.Length > 0)
            {
                result += $". Список: {listPart}";
            }
        }
        else if (requestedLogin is not null)
        {
            result = $"Пользователь {requestedLogin} не найден";
        }

        return result;
    }

    private async Task<(TwitchUser? User, string? RequestedLogin)> ResolveTargetUserAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken
    )
    {
        (TwitchUser? User, string? RequestedLogin) result = (null, null);

        if (
            parameters.TryGetValue("displayName", out var displayNameObj)
            && displayNameObj is string displayName
        )
        {
            var login = displayName.TrimStart('@');

            result = (
                await ensureService.EnsureUserExistsByLoginAsync(login, cancellationToken),
                login
            );
        }
        else if (parameters.TryGetValue("user", out var userObj) && userObj is TwitchUser user)
        {
            result = (user, null);
        }

        return result;
    }

    private static string BuildListPart(IReadOnlyList<(string Name, int Count)> items)
    {
        var result = "";

        if (items.Count > 0)
        {
            var joined = string.Join(", ", items.Select(item => $"{item.Name} ×{item.Count}"));

            result = joined.Length <= MaxListLength ? joined : $"{joined[..MaxListLength]}…";
        }

        return result;
    }
}