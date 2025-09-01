using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TwitchSubRecCommand(EventSubService eventSubService) : BaseCommand
{
    public override string CommandName => "twitchsubrec";
    public override string Description => "Выполняет реконнект EventSub Twitch";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var force = parameters.TryGetValue("force", out var forceParam) && (bool)forceParam;
            var result = await eventSubService.ResubscribeToEventSub(force: force);
            return result;
        }
        catch (Exception ex)
        {
            return $"Ошибка при реконекте EventSub: {ex.Message}";
        }
    }

    public override CommandParameterInfo[] Parameters => [
        new() {
            Name = "force",
            Description = "Принудительно выполнить реконект, даже если он уже выполняется",
            Type = "bool",
            Required = false,
            DefaultValue = "false"
        }
    ];
}

