using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class ShutdownCommand(IHostApplicationLifetime appLifetime, ILogger<ShutdownCommand> logger)
    : BaseCommand
{
    public override string CommandName => "shutdown";
    public override string Description => "Немедленно инициирует завершение работы приложения";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override string[] Aliases => ["stop", "exit", "kill"];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Инициирована остановка приложения. До встречи!";

        try
        {
            logger.LogWarning("Команда завершения работы запущена. Платформа: {Platform}", platform);
            appLifetime.StopApplication();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при попытке завершить приложение");
            result = $"Не удалось остановить приложение: {ex.Message}";
        }

        return Task.FromResult(result);
    }
}


