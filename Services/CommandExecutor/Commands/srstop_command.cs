using System.Collections.Generic;
using System.Threading;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrStopCommand(CommandsService commandsService) : BaseCommand
{
    public override string CommandName => "srstop";
    public override string Description => "Остановить воспроизведение SoundRequest";
    public override bool IsAdminCommand => true;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        return await commandsService.StopPlaybackAsync(cancellationToken);
    }
}
