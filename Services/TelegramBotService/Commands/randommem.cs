using MARS.Server.Services.RandomMem;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [Description("Включает или выключает онлайн-режим рандомных мемов")]
    [Admin]
    public async Task<Message> OnRandomMemOnlineCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var usage = RandomMemOnline.IsStop
            ? "Включил рандом мем онлайн!"
            : "Выключил рандом мем онлайн!";

        RandomMemOnline.IsStop = !RandomMemOnline.IsStop;

        return await botClient.SendMessage(
            message.Chat.Id,
            usage,
            cancellationToken: cancellationToken,
            replyParameters: message.MessageId
        );
    }
}
