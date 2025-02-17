using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnHonkaiUsersCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        var players = await dbContext
            .TelegramUsers.Where(e => e.HonkaiNotifications)
            .ToListAsync(cancellationToken: cancellationToken);

        return await botClient.SendMessage(
            message.Chat.Id,
            string.Join(",", players.Select(p => $"{p.UserId}({p.Name})")),
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
