using System.Collections.Generic;
using System.Threading;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrPlayCommand(SoundRequestCommandsService soundRequestCommandsService) : BaseCommand
{
    public override string CommandName => "srplay";
    public override string Description => "Возобновить воспроизведение SoundRequest";
    public override bool IsAdminCommand => true;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        return await soundRequestCommandsService.ResumePlaybackAsync(cancellationToken);
    }
}
