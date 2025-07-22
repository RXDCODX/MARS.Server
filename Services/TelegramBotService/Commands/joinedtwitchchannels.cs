using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [Description("Показывает список Twitch-каналов, к которым подключён бот")]
    public async Task<Message> OnJoinedTwitchChannelsCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var channels = client.JoinedChannels;

        return await botClient.SendMessage(
            message.Chat.Id,
            string.Join(Environment.NewLine, channels.Select(e => e.Channel)),
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
