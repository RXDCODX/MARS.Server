using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.SoundBarService;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class unmutesound_command(SoundMuteCoordinator coordinator) : BaseCommand
{
    public override string CommandName => "unmutesound";
    public override string Description => "Включить звук на компухтере";
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
            await coordinator.UnmuteAsync();
            return "Комплюхтер успешно размучен";
        }
        catch
        {
            return "Комплюхтер не удалось размутить";
        }
    }
}
