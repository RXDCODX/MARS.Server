namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnHonkaiCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var userName = message.Chat.Id;

        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        var dbUser = dbContext.TelegramUsers.Find(userName);

        if (dbUser == null || !dbUser.HonkaiNotifications)
        {
            if (dbUser == null)
            {
                dbUser = new TelegramUser
                {
                    HonkaiNotifications = true,
                    LastTimeMessage = DateTimeOffset.Now,
                    Name = message.Chat.Username!,
                    UserId = message.Chat.Id,
                };
                dbContext.TelegramUsers.Add(dbUser);
            }
            else
            {
                dbUser.HonkaiNotifications = true;
                dbContext.TelegramUsers.Update(dbUser);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);

                return await botClient.SendMessage(
                    userName,
                    "Успешно добавлен в лист ежедневных уведомлений!",
                    messageThreadId: message.MessageThreadId,
                    replyParameters: message.MessageId,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception e)
            {
                return await botClient.SendMessage(
                    userName,
                    $"Не удалось добавить уведомления об отметках Honkai :-( {e.Message}",
                    messageThreadId: message.MessageThreadId,
                    replyParameters: message.MessageId,
                    cancellationToken: cancellationToken
                );
            }
        }

        return await botClient.SendMessage(
            userName,
            "Ты уже в списке!",
            messageThreadId: message.MessageThreadId,
            replyParameters: message.MessageId,
            cancellationToken: cancellationToken
        );
    }
}
