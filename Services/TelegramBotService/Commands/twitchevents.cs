using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [AdminAtribbute]
    public async Task<Message> OnTwitchEventsCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var response = await userAuthHelper.GetEventSubs();

        if (response == null)
            return await botClient.SendMessage(
                message.Chat.Id,
                "Не удалось провести запрос",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken
            );

        var subs = response.Subscriptions.Select(e => $"{e.Type} - {e.Status}");

        var text =
            $"Подключенные сабы твича:{Environment.NewLine} {string.Join(Environment.NewLine, subs)}";

        return await botClient.SendMessage(
            message.Chat.Id,
            text,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
