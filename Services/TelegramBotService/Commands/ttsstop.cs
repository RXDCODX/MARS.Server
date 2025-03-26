namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnTTSStopCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        await syntheziaVoicer.Stop();

        const string usage = "Остановил все ттс фразы!";

        return await botClient.SendMessage(
            message.Chat.Id,
            usage,
            cancellationToken: cancellationToken,
            replyParameters: message.MessageId
        );
    }
}
