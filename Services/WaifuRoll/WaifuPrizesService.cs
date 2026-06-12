using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.Telegram;
using MARS.Server.Services.WaifuRoll.Entitys;
using MARS.Server.Services.WaifuRoll.helpers;
using MARS.Server.Services.WaifuRoll.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuPrizesService(
    IDbContextFactory<AppDbContext> factory,
    IOptions<ShikimoriClientOptions> shikiOptions,
    WaifuRollEnsurenceService waifuDbHelper,
    IHostApplicationLifetime lifetime
) : ITelegramusService, IWaifuPrizesService
{
    private string ShikimoriSite => shikiOptions.Value.ShikimoriSite;

    public async Task<OperationResult<ICollection<PrizeType>>> GetWaifuPrizesAsync()
    {
        var result = OperationResult<ICollection<PrizeType>>.Bad(
            "Ошибка при получении призов вайфу"
        );

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var prizes = new List<PrizeType>();

            var cancellationToken = lifetime.ApplicationStopping;
            const int batchSize = 50;
            var offset = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var waifusBatch = await dbContext
                    .Waifus.Where(e => !e.IsPrivated)
                    .AsNoTracking()
                    .OrderBy(e => e.ShikiId)
                    .Skip(offset)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (waifusBatch.Count == 0)
                {
                    break;
                }

                foreach (var waifu in waifusBatch)
                {
                    // Убеждаемся, что поля аниме и манги заполнены
                    var waifuWithTitles = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(
                        waifu,
                        dbContext
                    );

                    prizes.Add(
                        new PrizeType
                        {
                            Id = waifuWithTitles.ShikiId,
                            Image = ShikimoriSite + "/" + waifuWithTitles.ImageUrl,
                            Text = waifuWithTitles.Name,
                        }
                    );
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                offset += batchSize;
            }

            result = OperationResult<ICollection<PrizeType>>.Ok(
                "Призы вайфу успешно получены",
                prizes
            );
        }
        catch (Exception ex)
        {
            result = OperationResult<ICollection<PrizeType>>.Bad(
                $"Ошибка при получении призов вайфу: {ex.Message}"
            );
        }

        return result;
    }
}
