using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [Admin]
    public async Task<Message> OnScrupFrameDataCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        const string usage = "Парсинг запущен";

        await Task.Factory.StartNew(
            async () =>
            {
                await frameData.StartScrupFrameData(message.Chat).ConfigureAwait(false);
            },
            cancellationToken
        );

        return await botClient.SendMessage(
            message.Chat.Id,
            usage,
            cancellationToken: cancellationToken,
            parseMode: ParseMode.Html,
            replyParameters: message.MessageId
        );
    }
}
