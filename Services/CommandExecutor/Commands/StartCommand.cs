using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class StartCommand : BaseCommand
{
    public override string CommandName => "start";
    public override string Description =>
        "Показывает приветственное сообщение и основную информацию о боте";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];

    public override CommandVisibility Visibility => CommandVisibility.All; // Видна везде

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var usage = $"""
            Создатель бота - https://www.twitch.tv/{TwitchExstension.Channel}
            Бот используется как проводник медиафайлов с последующим отображением на стриме
            Больше инфы - /help, /whitelist, /commands
            """;

        return Task.FromResult(usage);
    }
}

