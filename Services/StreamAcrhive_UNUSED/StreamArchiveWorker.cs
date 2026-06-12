using System;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.StreamAcrhive_UNUSED.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.StreamAcrhive_UNUSED;

public class StreamArchiveWorker(
    IStreamArchiveService streamArchiveService,
    ILogger<StreamArchiveWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Запуск StreamArchiveWorker");

        try
        {
            await streamArchiveService.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("StreamArchiveWorker остановлен по запросу");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка в StreamArchiveWorker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Остановка StreamArchiveWorker");
        await streamArchiveService.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
