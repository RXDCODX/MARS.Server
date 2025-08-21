using MARS.Server.Services.WaifuRoll.Entitys.Interfaces;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuRollGuaranteeService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<WaifuRollGuaranteeService> logger
) : IWaifuRollGuaranteeService
{
    // Константы для настройки системы гарантов
    private const int GuaranteeRolls = 200; // Количество роллов для гаранта
    private const double VipChance = 0.15; // 15 сотых = 0.15 = 15%

    public async Task<bool> CheckVipDropAsync(string twitchId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var guarantee = await dbContext
                .WaifuRollGuarantees.AsNoTracking()
                .FirstOrDefaultAsync(g => g.TwitchId == twitchId);

            // Если пользователь достиг гаранта, VIP выпадает автоматически
            if (guarantee is { RollCount: >= GuaranteeRolls })
            {
                logger.LogInformation(
                    "VIP выпал по гаранту для пользователя {TwitchId} после {RollCount} роллов",
                    twitchId,
                    guarantee.RollCount
                );
                return true;
            }

            // Проверяем случайный шанс (15%)
            var random = Random.Shared.NextDouble();
            var vipDropped = random <= VipChance;

            if (vipDropped)
            {
                logger.LogInformation(
                    "VIP выпал по случайности для пользователя {TwitchId} (шанс: {Chance})",
                    twitchId,
                    VipChance
                );
            }

            return vipDropped;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при проверке выпадения VIP для пользователя {TwitchId}",
                twitchId
            );
            return false;
        }
    }

    public async Task<bool> IncrementRollCountAsync(string twitchId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var guarantee = await dbContext.WaifuRollGuarantees.FirstOrDefaultAsync(g =>
                g.TwitchId == twitchId
            );

            if (guarantee == null)
            {
                // Создаем новую запись для пользователя
                guarantee = new WaifuRollGuarantee
                {
                    TwitchId = twitchId,
                    RollCount = 1,
                    LastRoll = DateTimeOffset.Now,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now,
                };

                await dbContext.WaifuRollGuarantees.AddAsync(guarantee);
            }
            else
            {
                // Увеличиваем счетчик роллов
                guarantee.RollCount++;
                guarantee.LastRoll = DateTimeOffset.Now;
                guarantee.UpdatedAt = DateTimeOffset.Now;
            }

            await dbContext.SaveChangesAsync();

            logger.LogDebug(
                "Увеличен счетчик роллов для пользователя {TwitchId}: {RollCount}",
                twitchId,
                guarantee.RollCount
            );

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при увеличении счетчика роллов для пользователя {TwitchId}",
                twitchId
            );
            return false;
        }
    }

    public async Task<WaifuRollGuarantee?> GetGuaranteeInfoAsync(string twitchId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            return await dbContext
                .WaifuRollGuarantees.AsNoTracking()
                .FirstOrDefaultAsync(g => g.TwitchId == twitchId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении информации о гаранте для пользователя {TwitchId}",
                twitchId
            );
            return null;
        }
    }

    public async Task<bool> ResetRollCountAsync(string twitchId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var guarantee = await dbContext.WaifuRollGuarantees.FirstOrDefaultAsync(g =>
                g.TwitchId == twitchId
            );

            if (guarantee != null)
            {
                guarantee.RollCount = 0;
                guarantee.UpdatedAt = DateTimeOffset.Now;

                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Сброшен счетчик роллов для пользователя {TwitchId}",
                    twitchId
                );
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при сбросе счетчика роллов для пользователя {TwitchId}",
                twitchId
            );
            return false;
        }
    }
}
