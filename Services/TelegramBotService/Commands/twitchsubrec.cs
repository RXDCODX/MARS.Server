using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [Description("Выполняет реконнект EventSub Twitch")]
    [AdminAttribute]
    public async Task<Message> OnTwitchSubRecCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        await Task.Factory.StartNew(
            async () => await eventSubService.ResubscribeToEventSub(),
            cancellationToken
        );
        const string text = "Отправлена попытка реконекта";

        return await botClient.SendMessage(
            message.Chat.Id,
            text,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
