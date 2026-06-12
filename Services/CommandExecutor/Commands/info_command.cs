using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class InfoCommand() : BaseCommand
{
    public override string CommandName => "info";
    public override string Description =>
        "Показывает справку по возможностям телеграм бота и форматам медиа";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return Task.FromResult(GetGeneralHelp());
        }
        catch (Exception exception)
        {
            return Task.FromException<string>(exception);
        }
    }

    private static char[] GetCommandPrefixesForPlatform(Platform platform)
    {
        char[] result = platform switch
        {
            Platform.Twitch => ['!'],
            Platform.Telegram => ['/'],
            _ => ['/', '!'],
        };

        return result;
    }

    private static string GetGeneralHelp()
    {
        const string result = """
            Этот бот - проводник высокоинтерактивного контента на стриме https://twitch.tv/rxdcodx. 
            Все что будет отправлено тут - будет показано на стриме (если ты есть в белом списке).

            Можно отправлять:
            1) Войсы (там прикольная картинка со звуком перед воиспроизведением)
            2) Стикеры, на анимированные стикеры (в формате tgs) распростроняется кулдаун
            3) Видео до 20 мб в формате webm/mp4
            4) Аудио, но не советую. В них нету смысла, на стриме есть саундреквест
            5) Различные картинки, советую брать пикчи до разрешения в 1920x1080, кинешь выше - сломаю колени
            """;

        return result;
    }
}
