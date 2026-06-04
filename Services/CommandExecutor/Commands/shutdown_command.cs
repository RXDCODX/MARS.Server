namespace MARS.Server.Services.CommandExecutor.Commands;

public class ShutdownCommand : BaseCommand
{
    public override string CommandName => "shutdown";
    public override string Description => "Выключение сервиса";
    public override bool IsAdminCommand => true;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        // Простое сообщение, реальное завершение выполняется в другом месте
        return Task.FromResult("Сервер будет остановлен");
    }
}
