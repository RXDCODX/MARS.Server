namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnByeByeCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var userName = message.Chat.Id;
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        var dbUser = await dbContext.TelegramUsers.FindAsync(userName);

        if (dbUser == null || !dbUser.ByeByeServiceNotification)
        {
            if (dbUser == null)
            {
                dbUser = new TelegramUser
                {
                    Name = message.Chat.Username!,
                    UserId = message.Chat.Id,
                    ByeByeServiceNotification = true,
                };
                dbContext.TelegramUsers.Add(dbUser);
            }
            else
            {
                dbUser.ByeByeServiceNotification = true;
                dbContext.TelegramUsers.Update(dbUser);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);

                return await botClient.SendMessage(
                    userName,
                    "Успешно добавлен в лист ежедневных пожеланий спокойной ночи!",
                    messageThreadId: message.MessageThreadId,
                    replyParameters: message.MessageId,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception e)
            {
                return await botClient.SendMessage(
                    userName,
                    $"Не удалось добавить в лист ежедневных пожеланий спокойной ночи! :-( {e.Message}",
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
