using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

/// <summary>
/// Команда для краткого вывода списка команд без описаний
/// </summary>
public class CShortCommandsCommand : BaseCommand
{
    public override string CommandName => "c";
    public override string Description => "Показывает краткий список доступных команд без описаний";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Twitch];

    public override CommandVisibility Visibility => CommandVisibility.None; // Скрываем саму команду из списков

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        // Эта команда обрабатывается на уровне платформенных сервисов
        // и не должна вызываться напрямую
        return Task.FromResult("Команда обрабатывается на уровне платформы");
    }
}
