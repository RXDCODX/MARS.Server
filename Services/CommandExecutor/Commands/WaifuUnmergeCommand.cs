using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class WaifuUnmergeCommand(MergeWaifu mergeWaifu) : BaseCommand
{
    public override string CommandName => "waifuunmerge";
    public override string Description => "Развести супругов";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];
    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "identifier",
                Description = "ID или имя хоста",
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
        if (!parameters.TryGetValue("identifier", out var identifierObj))
        {
            return "Необходимо указать ID или имя хоста";
        }

        var identifier = identifierObj.ToString() ?? "";

        var isId = int.TryParse(identifier, out var id);
        var (waifu, host) = isId
            ? await mergeWaifu.Unmerge(id)
            : await mergeWaifu.Unmerge(identifier);

        return host is null ? "Не удалось найти этого хоста"
            : waifu is null ? $"Не удалось найти вайфу этого мужичка ({host.TwitchId}:{host.Name})"
            : $"Развод между {host.Name} и {waifu.Name} состоялся";
    }
}
