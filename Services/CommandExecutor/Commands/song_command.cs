using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SongCommand : BaseCommand
{
    public override string CommandName => "song";
    public override string Description => "Информация о песне";
    public override bool IsAdminCommand => false;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult("Информация о песне");
    }
}
