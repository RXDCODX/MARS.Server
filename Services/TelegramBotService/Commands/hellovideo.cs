using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnHelloVideoCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var split = message.Text?.Split(' ');

        if (split?.Length == 2)
        {
            var name = await helloVideoWorker.TestVideo(split[1]);

            if (name != null)
            {
                return await botClient.SendMessage(
                    message.Chat.Id,
                    $"Отправл приветствующий видос на имя {name}",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken
                );
            }
        }
        else if (split?.Length == 3)
        {
            var name = await helloVideoWorker.TestVideo(split[1], split[2]);

            if (name != null)
            {
                return await botClient.SendMessage(
                    message.Chat.Id,
                    $"Отправл приветствующий видос на имя {name} с цветом {split[2]}",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken
                );
            }
        }

        return await botClient.SendMessage(
            message.Chat.Id,
            "Кривые параметры",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
