using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Synthesizer;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TtsFilterCommand(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ITtsMessageFilterService ttsFilterService
) : BaseCommand
{
    public override string CommandName => "ttsfilter";
    public override string Description =>
        "Включает или выключает фильтрацию дубликатов TTS сообщений";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось переключить фильтр TTS";

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rootState = await db.RootState.FirstOrDefaultAsync(
            e => e.Name == RootStateKeys.TtsFilterEnabled,
            cancellationToken
        );

        if (rootState is not null)
        {
            var currentState = bool.TryParse(rootState.Value, out var isEnabled) && isEnabled;
            var nextState = !currentState;
            rootState.Value = nextState.ToString();
            ttsFilterService.IsFilterEnabled = nextState;

            await db.SaveChangesAsync(cancellationToken);

            result = nextState ? "Фильтр дубликатов TTS включён" : "Фильтр дубликатов TTS выключен";
        }
        else
        {
            result = "Переменная TtsFilterEnabled не найдена";
        }

        return result;
    }
}
