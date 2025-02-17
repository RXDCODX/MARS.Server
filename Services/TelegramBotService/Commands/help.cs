using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnHelpCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
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

        return await botClient.SendMessage(
            message.Chat.Id,
            usage,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
