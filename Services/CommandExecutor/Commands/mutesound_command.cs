using System.Collections.Generic;
using System.Threading;
using MARS.Server.Services.SoundBarService;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class mutesound_command(SoundMuteCoordinator coordinator) : BaseCommand
{
    public override string CommandName => "mutesound";
    public override string Description => "Выключить звук на компухтере";
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
            await coordinator.MuteAsync();
            return "Комплюхтер замучен успешно";
        }
        catch
        {
            return "Комплюхтер не удалось замутить";
        }
    }
}
