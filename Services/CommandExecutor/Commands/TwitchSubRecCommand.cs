using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TwitchSubRecCommand(EventSubService eventSubService) : BaseCommand
{
    public override string CommandName => "twitchsubrec";
    public override string Description => "Выполняет реконнект EventSub Twitch";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await eventSubService.ResubscribeToEventSubAsync();
            return result;
        }
        catch (Exception ex)
        {
            return $"Ошибка при реконекте EventSub: {ex.Message}";
        }
    }
}
