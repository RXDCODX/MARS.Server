namespace MARS.Server.Services.CommandExecutor.Commands;

public class HonkaiusersCommand(IDbContextFactory<AppDbContext> factory) : BaseCommand
{
    public override string CommandName => "honkaiusers";
    public override string Description =>
        "Показывает список пользователей с ежедневными уведомлениями Honkai";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        var players = await dbContext
            .TelegramUsers.Where(e => e.HonkaiNotifications)
            .ToListAsync(cancellationToken);

        return players.Count == 0
            ? "Нет пользователей с ежедневными уведомлениями Honkai"
            : string.Join(", ", players.Select(p => $"{p.UserId}({p.Name})"));
    }
}
