using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SysteminfoSystemInfoCommand(
    ILogger<SystemInfoCommand> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration
) : BaseCommand
{
    public override string CommandName => "systeminfo";
    public override string Description => "Показывает информацию о системе и сервисах";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override string[] Aliases => ["sysinfo"];

    public override CommandVisibility Visibility => CommandVisibility.FullList; // Скрываем из краткого списка

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            logger.LogInformation("Выполняется команда SystemInfo");

            var info = new List<string>
            {
                "🖥️ **Информация о системе**",
                $"Время запуска: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Версия .NET: {Environment.Version}",
                $"ОС: {Environment.OSVersion}",
                $"Процессоры: {Environment.ProcessorCount}",
                $"Память: {GC.GetTotalMemory(false) / 1024 / 1024} MB",
                "",
            };

            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );
                await dbContext.Database.CanConnectAsync(cancellationToken);
                info.Add("✅ База данных: Подключена");
            }
            catch (Exception ex)
            {
                info.Add($"❌ База данных: Ошибка подключения - {ex.Message}");
            }

            var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Unknown";
            info.Add($"🌍 Окружение: {environment}");

            return string.Join(Environment.NewLine, info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении команды SystemInfo");
            return $"❌ Ошибка: {ex.Message}";
        }
    }
}
