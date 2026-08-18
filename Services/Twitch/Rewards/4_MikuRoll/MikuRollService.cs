using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.Rewards._4_MikuRoll;

public class MikuRollService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<MikuModule?> RollTheMiku()
    {
        MikuModule? result = null;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var module = await dbContext.Miku.OrderBy(e => e.LastOrder).FirstOrDefaultAsync();

            if (module != null)
            {
                module.OrderCount++;
                module.LastOrder = DateTime.Now;

                dbContext.Miku.Update(module);
                await dbContext.SaveChangesAsync();

                result = module;
            }
        }
        catch
        {
            // Ошибка при ролле - возвращаем null
        }

        return result;
    }

    public async Task<MikuModule?> GetNextMikuModuleAsync(
        CancellationToken cancellationToken = default
    )
    {
        MikuModule? result = null;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            result = await dbContext
                .Miku.AsNoTracking()
                .OrderBy(e => e.LastOrder)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch
        {
            // Ошибка при получении модуля
        }

        return result;
    }

    public async Task<OperationResult<ICollection<MikuPrizeType>>> GetMikuPrizesAsync()
    {
        var result = OperationResult<ICollection<MikuPrizeType>>.Bad(
            "Ошибка при получении призов MikuModule"
        );

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var prizes = new List<MikuPrizeType>();

            var modules = await dbContext.Miku.AsNoTracking().OrderBy(e => e.PageId).ToListAsync();

            foreach (var module in modules)
            {
                prizes.Add(
                    new MikuPrizeType
                    {
                        Id = module.PageId,
                        Image = module.ThumbnailUrl,
                        Text = module.JapaneseName ?? module.Title,
                    }
                );
            }

            result = OperationResult<ICollection<MikuPrizeType>>.Ok(
                "Призы MikuModule успешно получены",
                prizes
            );
        }
        catch (Exception ex)
        {
            result = OperationResult<ICollection<MikuPrizeType>>.Bad(
                $"Ошибка при получении призов MikuModule: {ex.Message}"
            );
        }

        return result;
    }
}
