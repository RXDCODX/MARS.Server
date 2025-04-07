using MARS.Server.Services.RandomMem.Entity;

namespace MARS.Server.Services.RandomMem;

public class RandomMemeWorker(
    IDbContextFactory<AppDbContext> contextFactory,
    IWebHostEnvironment webHostEnvironment
) : BackgroundService
{
    private readonly string folderPath = Path.Combine(webHostEnvironment.WebRootPath, "Alerts");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //TODO: добавить миграцию
        await Task.Factory.StartNew(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await using var dbContext = await contextFactory.CreateDbContextAsync(
                        stoppingToken
                    );

                    var files = Directory
                        .GetFiles(folderPath, "*", SearchOption.AllDirectories)
                        .ToHashSet();
                    var orders = await dbContext.RandomMemeOrder.ToListAsync(stoppingToken);

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

                    // Ставим тип мема для файлов без типа
                    var memeTypes = await dbContext
                        .RandomMemeType.AsNoTracking()
                        .OrderByDescending(e => e.FolderPath.Length)
                        .ToArrayAsync(stoppingToken);

                    foreach (var memeOrder in orders.Where(e => e.MemeTypeId is null))
                    {
                        foreach (var memeType in memeTypes)
                        {
                            if (
                                !memeOrder.FilePath.Contains(
                                    memeType.FolderPath,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                continue;
                            }

                            memeOrder.MemeTypeId = memeType.Id;
                            break;
                        }
                    }

                    // Добавляем новые файлы в конец очереди и пересчитываем их MemeOrder.Order
                    var newFiles = files.Except(fileNamesInDb).ToArray();
                    Random.Shared.Shuffle(newFiles);
                    if (newFiles.Any())
                    {
                        foreach (var type in memeTypes)
                        {
                            var typedOrders = orders
                                .Where(e => e.MemeTypeId == type.Id)
                                .OrderBy(e => e.Order)
                                .ToArray();

                            var typedNewFiles = newFiles
                                .Where(e =>
                                    e.Contains(type.FolderPath, StringComparison.OrdinalIgnoreCase)
                                )
                                .ToArray();

                            newFiles = newFiles.Except(typedNewFiles).ToArray();

                            var counter = 1;

                            foreach (var typedOrder in typedOrders)
                            {
                                typedOrder.Order = counter;
                                checked
                                {
                                    counter++;
                                }
                            }

                            var newOrders = typedNewFiles
                                .Select(
                                    (file, index) =>
                                        new MemeOrder
                                        {
                                            FilePath = file,
                                            Order = counter + index,
                                            MemeTypeId = type.Id,
                                        }
                                )
                                .ToList();
                            dbContext.RandomMemeOrder.AddRange(newOrders);
                        }

                        var cunter = 1;
                        dbContext.RandomMemeOrder.AddRange(
                            newFiles.Select(
                                (a) => new MemeOrder() { FilePath = a, Order = cunter++ }
                            )
                        );
                    }

                    if (missingFiles.Any() || newFiles.Any())
                    {
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            },
            TaskCreationOptions.LongRunning
        );
    }
}
