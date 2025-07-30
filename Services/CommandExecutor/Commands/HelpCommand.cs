using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class HelpCommand : BaseCommand
{
    public override string CommandName => "help";
    public override string Description =>
        "Показывает справку по возможностям бота и форматам медиа";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        const string usage = """
            Можно отправлять:
            1) Войсы
            2) Стикеры, на анимированные стикеры (в формате tgs) распростроняется кулдаун
            3) Видео до 20 мб в формате webm/mp4
            4) Аудио, но не советую. В них нету смысла, на стриме есть саундреквест
            5) Различные картинки, советую брать пикчи до разрешения в 1920x1080, кинешь выше - сломаю колени
            """;

        return Task.FromResult(usage);
    }
}
