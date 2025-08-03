using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class ByebyeCommand : BaseCommand
{
    public override string CommandName => "byebye";
    public override string Description => "Прощание с пользователем";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Discord, Platform.Vk, Platform.Twitch];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        const string usage = """
            Пока-пока! 👋
            Надеюсь, ты скоро вернешься! 😊
            """;

        return Task.FromResult(usage);
    }
}
