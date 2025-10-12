using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class ZoneZeroCommand(IDbContextFactory<AppDbContext> factory) : BaseCommand
{
    public override string CommandName => "zonezero";
    public override string Description =>
        "Показывает информацию о ежедневных уведомлениях Zenless Zone Zero";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Telegram];
    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "chatId",
                Description = "Id телеграм чата",
                Type = "long",
                Required = true,
            },
            new()
            {
                Name = "username",
                Description = "Имя пользователя",
                Type = "string",
                Required = true,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var chatId = long.Parse((string)parameters["chatId"]);
        var userName = (string)parameters["username"];

        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        var dbUser = await dbContext.TelegramUsers.FindAsync([userName], cancellationToken);

        switch (dbUser)
        {
            case { HonkaiNotifications: true }:
                return "Ты уже в списке!";
            case null:
                dbUser = new TelegramUser
                {
                    HonkaiNotifications = true,
                    LastTimeMessage = DateTimeOffset.Now,
                    Name = userName,
                    UserId = chatId,
                };
                dbContext.TelegramUsers.Add(dbUser);
                break;
            default:
                dbUser.HonkaiNotifications = true;
                dbContext.TelegramUsers.Update(dbUser);
                break;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return "Успешно добавлен в лист ежедневных уведомлений!";
        }
        catch (Exception e)
        {
            return $"Не удалось добавить уведомления об отметках Honkai :-( {e.Message}";
        }
    }
}
