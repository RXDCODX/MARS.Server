using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [Description("Показывает рабочую директорию сервера")]
    public async Task<Message> OnDirectoryCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var directory = environment.WebRootPath;

        return await botClient.SendMessage(
            message.Chat.Id,
            directory,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
