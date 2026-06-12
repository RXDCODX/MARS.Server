using System.Collections.Generic;
using System.Threading;
using MARS.Server.Services.Twitch.Client;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TwitchchannelstatusCommand(TwitchConnectionManager manager) : BaseCommand
{
    public override string CommandName => "twitchchannelstatus";
    public override string Description => "Показывает состояние подключения Twitch-чата";
    public override bool IsAdminCommand => true;
    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(manager.GetStatus());
    }
}
