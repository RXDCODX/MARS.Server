namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnGenshinCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var userName = message.Chat.Id;
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        var dbUser = dbContext.TelegramUsers.Find(userName);

        if (dbUser == null || !dbUser.ZenlessZoneZeroDailyNotif)
        {
            if (dbUser == null)
            {
                dbUser = new TelegramUser
                {
                    HonkaiNotifications = false,
                    LastTimeMessage = DateTimeOffset.Now,
                    Name = message.Chat.Username!,
                    PyroAlertsAccess = false,
                    RaidHelper = false,
                    StreamUpNotifications = false,
                    UserId = message.Chat.Id,
                    ZenlessZoneZeroDailyNotif = false,
                    GenshinImpactDailyNotif = true,
                };
                dbContext.TelegramUsers.Add(dbUser);
            }
            else
            {
                dbUser.GenshinImpactDailyNotif = true;
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
                    $"Не удалось добавить уведомления об отметках в Genshin Impact :-( {e.Message}",
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
