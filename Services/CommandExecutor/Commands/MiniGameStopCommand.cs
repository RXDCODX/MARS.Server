using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Rewards.MiniGames;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class MiniGameStopCommand(MiniGamesManager manager) : BaseCommand
{
    public override string CommandName => "minigamestop";
    public override string Description => "Остановка миниигр твича";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        await manager.StopAsync(cancellationToken);
        return "Все миниигры были принудительно остановлены!";
    }
}
