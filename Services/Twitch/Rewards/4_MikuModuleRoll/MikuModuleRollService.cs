using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.Rewards._4_MikuModuleRoll;

public class MikuModuleRollService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<MikuModule?> RollTheMikuModule()
    {
        MikuModule? result = null;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var module = await dbContext
                .MikuModules.OrderBy(e => e.LastOrder)
                .FirstOrDefaultAsync();

            if (module != null)
            {
                module.OrderCount++;
                module.LastOrder = DateTime.Now;

                dbContext.MikuModules.Update(module);
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

    public async Task<OperationResult<ICollection<MikuModulePrizeType>>> GetMikuModulePrizesAsync()
    {
        var result = OperationResult<ICollection<MikuModulePrizeType>>.Bad(
            "Ошибка при получении призов MikuModule"
        );

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var prizes = new List<MikuModulePrizeType>();

            var modules = await dbContext
                .MikuModules.AsNoTracking()
                .OrderBy(e => e.PageId)
                .ToListAsync();

            foreach (var module in modules)
            {
                prizes.Add(
                    new MikuModulePrizeType
                    {
                        Id = module.PageId,
                        Image = module.ThumbnailUrl,
                        Text = module.JapaneseName ?? module.Title,
                    }
                );
            }

            result = OperationResult<ICollection<MikuModulePrizeType>>.Ok(
                "Призы MikuModule успешно получены",
                prizes
            );
        }
        catch (Exception ex)
        {
            result = OperationResult<ICollection<MikuModulePrizeType>>.Bad(
                $"Ошибка при получении призов MikuModule: {ex.Message}"
            );
        }

        return result;
    }
}
