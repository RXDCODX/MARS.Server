using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class GenshinCommand : BaseCommand
{
    public override string CommandName => "genshin";
    public override string Description =>
        "Показывает информацию о ежедневных уведомлениях Genshin Impact";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        const string usage = """
            Ежедневные уведомления Genshin Impact:

            Для подключения уведомлений используйте Telegram бота.
            В API эта функция недоступна из соображений безопасности.

            Уведомления отправляются автоматически.
            """;

        return Task.FromResult(usage);
    }
}
