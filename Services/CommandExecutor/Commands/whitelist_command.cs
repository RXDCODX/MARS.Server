using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class WhitelistCommand(IDbContextFactory<AppDbContext> factory) : BaseCommand
{
    public override string CommandName => "whitelist";
    public override string Description => "Показывает список пользователей с доступом к PyroAlerts";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        var users = await dbContext
            .TelegramUsers.AsNoTracking()
            .Where(e => e.PyroAlertsAccess)
            .Select(e => e.Name)
            .ToListAsync(cancellationToken);

        return users.Count == 0
            ? "Список пользователей с доступом к PyroAlerts пуст"
            : "Пользователи с доступом к PyroAlerts:\n" + string.Join(Environment.NewLine, users);
    }
}
