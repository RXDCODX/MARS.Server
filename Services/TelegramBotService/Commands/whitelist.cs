using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnWhiteListCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        var usage = await dbContext
            .TelegramUsers.AsNoTracking()
            .Where(e => e.PyroAlertsAccess)
            .Select(e => e.Name)
            .ToListAsync(cancellationToken);

        return await botClient.SendMessage(
            message.Chat.Id,
            string.Join(Environment.NewLine, usage),
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
