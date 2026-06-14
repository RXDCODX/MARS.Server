using MARS.Server.DataBaseContext;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.Rewards._4_FumoRoll;

public class FumoRollService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<Fumo?> RollTheFumo()
    {
        Fumo? result = null;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var fumo = await dbContext.Fumos.OrderBy(e => e.LastOrder).FirstOrDefaultAsync();

            if (fumo != null)
            {
                fumo.OrderCount++;
                fumo.LastOrder = DateTimeOffset.Now;

                dbContext.Fumos.Update(fumo);
                await dbContext.SaveChangesAsync();

                result = fumo;
            }
        }
        catch
        {
            // Ошибка при ролле - возвращаем null
        }

        return result;
    }

    public async Task<OperationResult<ICollection<FumoPrizeType>>> GetFumoPrizesAsync()
    {
        var result = OperationResult<ICollection<FumoPrizeType>>.Bad(
            "Ошибка при получении призов Fumo"
        );

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var prizes = new List<FumoPrizeType>();

            var fumos = await dbContext.Fumos.AsNoTracking().OrderBy(e => e.MfcId).ToListAsync();

            foreach (var fumo in fumos)
            {
                prizes.Add(
                    new FumoPrizeType
                    {
                        Id = fumo.MfcId,
                        Image = fumo.ThumbnailUrl,
                        Text = fumo.Character,
                    }
                );
            }

            result = OperationResult<ICollection<FumoPrizeType>>.Ok(
                "Призы Fumo успешно получены",
                prizes
            );
        }
        catch (Exception ex)
        {
            result = OperationResult<ICollection<FumoPrizeType>>.Bad(
                $"Ошибка при получении призов Fumo: {ex.Message}"
            );
        }

        return result;
    }
}
