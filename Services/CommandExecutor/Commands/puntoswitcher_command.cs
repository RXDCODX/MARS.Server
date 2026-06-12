using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.PuntoSwitcher;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class PuntoSwitcherCommand(IDbContextFactory<AppDbContext> dbContextFactory) : BaseCommand
{
    public override string CommandName => "puntoswitcher";
    public override string Description =>
        "Включает или выключает фильтрацию сообщений пунтосвитчером";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось переключить PuntoSwitcher";

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rootState = await db.RootState.FirstOrDefaultAsync(
            e => e.Name == RootStateKeys.PuntoSwitcherFilterEnabled,
            cancellationToken
        );

        if (rootState is not null)
        {
            var currentState = bool.TryParse(rootState.Value, out var isEnabled) && isEnabled;
            var nextState = !currentState;
            rootState.Value = nextState.ToString();
            PuntoSwitcherState.IsFilterEnabled = nextState;

            await db.SaveChangesAsync(cancellationToken);

            result = nextState
                ? "PuntoSwitcher фильтрация включена"
                : "PuntoSwitcher фильтрация выключена";
        }
        else
        {
            result = "Переменная PuntoSwitcherFilterEnabled не найдена";
        }

        return result;
    }
}
