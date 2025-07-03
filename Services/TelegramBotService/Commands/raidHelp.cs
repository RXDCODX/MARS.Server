namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnRaidHelpCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        const string usage = """
            Ты уже состоишь в рейд-хелперах, спасибо!
            """;

        const string usage2 = """
            Добавил тебя в рейд-хелперов, спасибо!
            """;

        var userName = message.Chat.Id;
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        var dbUser = dbContext.TelegramUsers.Find(userName);

        if (dbUser == null || !dbUser.RaidHelper)
        {
            if (dbUser == null)
            {
                dbUser = new TelegramUser
                {
                    HonkaiNotifications = false,
                    LastTimeMessage = DateTimeOffset.Now,
                    Name = message.Chat.Username!,
                    PyroAlertsAccess = false,
                    RaidHelper = true,
                    StreamUpNotifications = false,
                    UserId = message.Chat.Id,
                };
                dbContext.TelegramUsers.Add(dbUser);
            }
            else
            {
                dbUser.RaidHelper = true;
                dbContext.TelegramUsers.Update(dbUser);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);

                return await botClient.SendMessage(
                    userName,
                    usage2,
                    messageThreadId: message.MessageThreadId,
                    replyParameters: message.MessageId,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception e)
            {
                return await botClient.SendMessage(
                    userName,
                    $"Не удалось добавить в рейд-хелперы :-( {e.Message}",
                    messageThreadId: message.MessageThreadId,
                    replyParameters: message.MessageId,
                    cancellationToken: cancellationToken
                );
            }
        }

        return await botClient.SendMessage(
            userName,
            usage,
            messageThreadId: message.MessageThreadId,
            replyParameters: message.MessageId,
            cancellationToken: cancellationToken
        );
    }
}
