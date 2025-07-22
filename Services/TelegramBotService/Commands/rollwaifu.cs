namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [Description("Выполняет вайфу-ролл для пользователя")]
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
                var resultRoll = await waifoRollService.TelegramRollWaifu(niname);

                if (resultRoll is { host: not null, waifu: not null })
                {
                    result =
                        $"Вайфу ролл с вайфучкой {resultRoll.waifu.Name} для {resultRoll.host.Name} выполнен!";
                    await alertsHub.Clients.All.WaifuRoll(
                        resultRoll.waifu,
                        resultRoll.host.Name ?? throw new NullReferenceException(),
                        resultRoll.husband
                    );
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
