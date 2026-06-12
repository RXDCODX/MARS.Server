using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class getAllKeyWordsForAlerts_Command(IDbContextFactory<AppDbContext> factory) : BaseCommand
{
    public override string CommandName => "getAllKeyWordsForAlerts";
    public override string Description => "Получить все ключевые слова для активации алертов";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        var sb = new StringBuilder();

        await foreach (
            var mediaInfo in dbContext
                .Alerts.AsNoTracking()
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
        )
        {
            if (!string.IsNullOrWhiteSpace(mediaInfo.TextInfo.TriggerWord))
            {
                sb.Append(mediaInfo.TextInfo.TriggerWord);
                sb.AppendLine();
                sb.Append('#');
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
