using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.Rewards._4_FrogRoll;

public class FrogRollService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<Frog?> RollTheFrog()
    {
        Frog? result = null;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var frog = await dbContext.Frogs.OrderBy(e => e.LastOrder).FirstOrDefaultAsync();

            if (frog != null)
            {
                frog.OrderCount++;
                frog.LastOrder = DateTime.Now;

                dbContext.Frogs.Update(frog);
                await dbContext.SaveChangesAsync();

                result = frog;
            }
        }
        catch
        {
            // Ошибка при ролле - возвращаем null
        }

        return result;
    }

    public async Task<OperationResult<ICollection<FrogPrizeType>>> GetFrogPrizesAsync()
    {
        var result = OperationResult<ICollection<FrogPrizeType>>.Bad(
            "Ошибка при получении призов Frog"
        );

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var prizes = new List<FrogPrizeType>();

            var frogs = await dbContext.Frogs.AsNoTracking().OrderBy(e => e.Pid).ToListAsync();

            foreach (var frog in frogs)
            {
                prizes.Add(
                    new FrogPrizeType
                    {
                        Id = frog.Pid,
                        Image = frog.ThumbnailUrl,
                        Text = frog.RussianName ?? frog.CommonName,
                    }
                );
            }

            result = OperationResult<ICollection<FrogPrizeType>>.Ok(
                "Призы Frog успешно получены",
                prizes
            );
        }
        catch (Exception ex)
        {
            result = OperationResult<ICollection<FrogPrizeType>>.Bad(
                $"Ошибка при получении призов Frog: {ex.Message}"
            );
        }

        return result;
    }
}
