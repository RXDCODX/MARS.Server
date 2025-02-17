namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnCatisaCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var userName = message.Chat.Id;
        var split = message?.Text?.Split(' ');

        if (split is { Length: < 3 })
            return await botClient.SendMessage(
                userName,
                "Кривые параметры котисы!",
                messageThreadId: message!.MessageThreadId,
                replyParameters: message.MessageId,
                cancellationToken: cancellationToken
            );
        var channel = split?[1];
        var text = split?.Skip(2).ToList();

        try
        {
            client.JoinChannel(channel, true);
            if (text != null)
                client.SendMessage(channel, string.Join(' ', text));

            return await botClient.SendMessage(
                userName,
                $"Сообщение на канал {channel} отправленно!",
                messageThreadId: message!.MessageThreadId,
                replyParameters: message.MessageId,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            return await botClient.SendMessage(
                userName,
                $"Ошибка отправления сообщения на канал {channel}! {ex.Message}",
                messageThreadId: message!.MessageThreadId,
                replyParameters: message.MessageId,
                cancellationToken: cancellationToken
            );
        }
    }
}
