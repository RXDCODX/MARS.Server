using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrPlayCommand(CommandsService commandsService) : BaseCommand
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
        return await commandsService.ResumePlaybackAsync(cancellationToken);
    }
}
