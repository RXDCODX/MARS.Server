using System.Collections.Generic;
using System.Threading;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SetenvCommand : BaseCommand
{
    public override string CommandName => "setenv";
    public override string Description => "Установить переменную окружения";
    public override bool IsAdminCommand => true;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult("Переменная окружения установлена");
    }
}
