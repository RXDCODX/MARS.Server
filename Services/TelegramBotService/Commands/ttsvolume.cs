using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [AdminAtribbute]
    public async Task<Message> OnTTSVolumeCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        string returnMessage;
        var split = message.Text?.Split(' ');

        if (split != null && split.Length == 2)
        {
            if (int.TryParse(split[1], out var volume))
            {
                if (OperatingSystem.IsWindows())
                {
                    syntheziaVoicer.ChangeVolume(volume);
                    returnMessage = $"Громкость была установленнна на {volume}";
                }
                else
                {
                    returnMessage = "Громкость не может быть изминена на Linux";
                }
            }
            else
                returnMessage = "Громкость должна быть натуральным числом!";
        }
        else
            returnMessage = "Кривые параметры!";

        return await botClient.SendMessage(
            message.Chat.Id,
            returnMessage,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
