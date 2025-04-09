namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [AdminAttribute]
    public async Task<Message> OnWaifuUnMergeCommandReceived(
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
            var rr = int.TryParse(niname, out var id);
            (Waifu? waifu, Host? host) = rr
                ? await mergeWaifu.Unmerge(id)
                : await mergeWaifu.Unmerge(niname);

            if (host is null)
            {
                result = "Не удалось найти этого хоста";
            }
            else if (waifu is null)
            {
                result = $"Не удалось найти вайфу этого мужичка ({host.TwitchId}:{host.Name})";
            }
            else
            {
                result = $"Развод между {host.Name} и {waifu.Name} состоялся";
            }
        }

        return await botClient.SendMessage(
            message.Chat,
            result,
            cancellationToken: cancellationToken
        );
    }
}
