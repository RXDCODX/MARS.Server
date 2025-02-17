using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnStartCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        const string usage = """
            Создатель бота - https://www.twitch.tv/pyrokxnezxz
            Бот используется как проводник медиафайлов с последующим отображением на стриме
            Больше инфы - /help, /whitelist, /commands
            """;

        return await botClient.SendMessage(
            message.Chat.Id,
            usage,
            cancellationToken: cancellationToken,
            parseMode: ParseMode.Html,
            replyParameters: message.MessageId
        );
    }
}
