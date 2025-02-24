using Hangfire;
using MARS.Server.Services.RandomMem.Entity;

namespace MARS.Server.Services.RandomMem;

public class RandomMemeWorker(
    ILogger<RandomMemeWorker> logger,
    IDbContextFactory<AppDbContext> contextFactory,
    RandomMemHandler randomMemHandler
)
{
    private readonly ILogger<RandomMemeWorker> _logger = logger;

    public async Task Process(CancellationToken stoppingToken)
    {
        await using AppDbContext dbContext = await contextFactory.CreateDbContextAsync(
            stoppingToken
        );
        var files = Directory
            .GetFiles(randomMemHandler.AlertsPath, "*", SearchOption.AllDirectories)
            .ToHashSet();
        var orders = await dbContext.RandomMemeOrder.AsNoTracking().ToListAsync(stoppingToken);

        var fileNamesInDb = orders.Select(o => o.FilePath).ToHashSet();

        // Remove missing files from queue
        var missingFiles = fileNamesInDb.Except(files).ToList();
        if (missingFiles.Any())
        {
            orders.RemoveAll(o => missingFiles.Contains(o.FilePath));

            dbContext.RandomMemeOrder.RemoveRange(
                await dbContext
                    .RandomMemeOrder.Where(o => missingFiles.Contains(o.FilePath))
                    .ToListAsync(stoppingToken)
            );
        }

        // Добавляем новые файлы в конец очереди и пересчитываем их VideoOrder.Order
        var newFiles = files.Except(fileNamesInDb).ToList();
        if (newFiles.Any())
        {
            var maxOrder = orders.Any() ? orders.Max(o => o.Order) : 0;
            var newOrders = newFiles
                .Select(
                    (file, index) =>
                        new VideoOrder { FilePath = file, Order = maxOrder + index + 1 }
                )
                .ToList();
            dbContext.RandomMemeOrder.AddRange(newOrders);
        }

        if (missingFiles.Any() || newFiles.Any())
        {
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}
