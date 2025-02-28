using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [AdminAtribbute]
    public async Task<Message> OnTwitchSubRecCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        await userAuthHelper.Reconnect(string.Empty);
        var text = "Отправлена попытка реконекта";

        return await botClient.SendMessage(
            message.Chat.Id,
            text,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
