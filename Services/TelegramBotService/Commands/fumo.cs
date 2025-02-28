namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [AdminAtribbute]
    public async Task<Message> OnFumoCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var splits = message.Text?.Split(' ');

        if (splits?.Length == 2)
        {
            var displayName = splits[1];

            await alertsHub.Clients.All.FumoFriday(displayName);

            return await botClient.SendMessage(
                message.Chat.Id,
                $"Фумо фрайдей с {displayName} объявлен!",
                cancellationToken: cancellationToken,
                replyParameters: message.MessageId
            );
        }

        if (splits?.Length == 3)
        {
            var displayName = splits[1];
            var color = splits[2];

            await alertsHub.Clients.All.FumoFriday(displayName, color);

            return await botClient.SendMessage(
                message.Chat.Id,
                $"Фумо фрайдей с {displayName} с цветом {color} объявлен!",
                cancellationToken: cancellationToken,
                replyParameters: message.MessageId
            );
        }

        return await botClient.SendMessage(
            message.Chat.Id,
            $"Кривые параметры для фумо фрайдея",
            cancellationToken: cancellationToken,
            replyParameters: message.MessageId
        );
    }
}
