namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnWaifuRollCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var splits = message.Text?.Split(' ');
        var result = "Плохой запрос";

        if (splits is { Length: 2 })
        {
            var niname = splits[1];

            try
            {
                var waifu = await waifoRollService.TelegramRollWaifu(niname);

                if (waifu is { host: not null, waifu: not null })
                {
                    result =
                        $"Вайфу ролл с вайфучкой {waifu.waifu.Name} для {waifu.host.Name} выполнен!";
                }
            }
            catch (Exception e)
            {
                result = e.Message;
            }
        }

        return await botClient.SendMessage(
            message.Chat.Id,
            result,
            cancellationToken: cancellationToken,
            replyParameters: message.MessageId
        );
    }
}
