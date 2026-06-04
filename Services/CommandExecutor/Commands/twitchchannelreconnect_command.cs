using MARS.Server.Services.Twitch.Client;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TwitchchannelreconnectCommand(TwitchConnectionManager manager) : BaseCommand
{
    public override string CommandName => "twitchchannelreconnect";
    public override string Description => "Выполняет реконнект к Twitch-чату";
    public override bool IsAdminCommand => true;
    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var ok = await manager.ReconnectAsync();
        return ok ? "Реконнект выполнен" : "Не удалось выполнить реконнект";
    }
}
