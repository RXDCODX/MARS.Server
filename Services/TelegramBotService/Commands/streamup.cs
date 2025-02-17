namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnStreamupCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var userName = message.Chat.Id;

        var user = new TelegramUser
        {
            UserId = message.Chat.Id,
            Name = message.Chat.Username!,
            LastTimeMessage = DateTimeOffset.Now,
            StreamUpNotifications = true,
        };
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        var dbUser = dbContext.TelegramUsers.Find(userName);

        if (dbUser == null || !dbUser.StreamUpNotifications)
        {
            if (dbUser == null)
                dbContext.TelegramUsers.Add(user);
            else
            {
                dbUser.StreamUpNotifications = true;
                dbContext.TelegramUsers.Update(dbUser);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);

                return await botClient.SendMessage(
                    userName,
                    "Уведомления о начале стрима подключены!",
                    messageThreadId: message.MessageThreadId,
                    replyParameters: message.MessageId,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception e)
            {
                return await botClient.SendMessage(
                    userName,
                    $"Не удалось добавить уведомления о начале стрима :-( {e.Message}",
                    messageThreadId: message.MessageThreadId,
                    replyParameters: message.MessageId,
                    cancellationToken: cancellationToken
                );
            }
        }

        return await botClient.SendMessage(
            userName,
            "Уведомления о начале стрима УЖЕ подключены!",
            messageThreadId: message.MessageThreadId,
            replyParameters: message.MessageId,
            cancellationToken: cancellationToken
        );
    }
}
