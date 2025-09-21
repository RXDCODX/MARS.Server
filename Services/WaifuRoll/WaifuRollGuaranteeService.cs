using MARS.Server.Services.WaifuRoll.Entitys.Interfaces;
using MARS.Server.Services.WaifuRoll.Models;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuRollGuaranteeService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<WaifuRollGuaranteeService> logger
) : IWaifuRollGuaranteeService
{
    // Константы для настройки системы гарантов
    private const int GuaranteeRolls = 200; // Количество роллов для гаранта
    private const double VipChance = 0.015; // Шанс = 1.5%

    public async Task<OperationResult<VipDropResponse>> CheckVipDropAsync(string twitchId)
    {
        var result = OperationResult<VipDropResponse>.Bad("Ошибка при проверке VIP статуса");
        
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var guarantee = await dbContext
                .WaifuRollGuarantees.AsNoTracking()
                .FirstOrDefaultAsync(g => g.TwitchId == twitchId);

            var vipResponse = new VipDropResponse
            {
                RollCount = guarantee?.RollCount ?? 0
            };

            // Если пользователь достиг гаранта, VIP выпадает автоматически
            if (guarantee is { RollCount: >= GuaranteeRolls })
            {
                logger.LogInformation(
                    "VIP выпал по гаранту для пользователя {TwitchId} после {RollCount} роллов",
                    twitchId,
                    guarantee.RollCount
                );
                
                vipResponse.IsVipDropped = true;
                vipResponse.DropReason = "Гарант";
                
                // Удаляем пользователя из системы гарантов
                var deleteResult = await DeleteGuaranteeAsync(twitchId);
                if (!deleteResult)
                {
                    logger.LogWarning("Не удалось удалить пользователя {TwitchId} из системы гарантов", twitchId);
                }
                
                result = OperationResult<VipDropResponse>.Ok("VIP выпал по гаранту", vipResponse);
            }
            else
            {
                // Проверяем случайный шанс (1.5%)
                var random = Random.Shared.NextDouble();
                vipResponse.IsVipDropped = random <= VipChance;

                if (vipResponse.IsVipDropped)
                {
                    logger.LogInformation(
                        "VIP выпал по случайности для пользователя {TwitchId} (шанс: {Chance})",
                        twitchId,
                        VipChance
                    );
                    
                    vipResponse.DropReason = "Случайность";
                    
                    // При случайном выпадении сбрасываем счетчик роллов
                    var resetResult = await ResetRollCountAsync(twitchId);
                    if (!resetResult)
                    {
                        logger.LogWarning("Не удалось сбросить счетчик роллов для пользователя {TwitchId}", twitchId);
                    }
                }
                else
                {
                    vipResponse.DropReason = "Не выпал";
                }
                
                result = OperationResult<VipDropResponse>.Ok("Проверка VIP статуса завершена", vipResponse);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при проверке выпадения VIP для пользователя {TwitchId}",
                twitchId
            );
            result = OperationResult<VipDropResponse>.Bad($"Ошибка при проверке VIP статуса: {ex.Message}");
        }
        
        return result;
    }

    public async Task<OperationResult<RollCountResponse>> IncrementRollCountAsync(string twitchId)
    {
        var result = OperationResult<RollCountResponse>.Bad("Ошибка при увеличении счетчика роллов");
        
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

            var rollCountResponse = new RollCountResponse
            {
                Success = true,
                CurrentRollCount = guarantee.RollCount,
                Message = "Счетчик роллов успешно увеличен"
            };

            result = OperationResult<RollCountResponse>.Ok("Счетчик роллов увеличен", rollCountResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при увеличении счетчика роллов для пользователя {TwitchId}",
                twitchId
            );
            result = OperationResult<RollCountResponse>.Bad($"Ошибка при увеличении счетчика: {ex.Message}");
        }
        
        return result;
    }

    public async Task<OperationResult<WaifuRollGuarantee?>> GetGuaranteeInfoAsync(string twitchId)
    {
        var result = OperationResult<WaifuRollGuarantee?>.Bad("Ошибка при получении информации о гаранте");
        
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var guarantee = await dbContext
                .WaifuRollGuarantees.AsNoTracking()
                .FirstOrDefaultAsync(g => g.TwitchId == twitchId);

            result = OperationResult<WaifuRollGuarantee?>.Ok("Информация о гаранте получена", guarantee);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении информации о гаранте для пользователя {TwitchId}",
                twitchId
            );
            result = OperationResult<WaifuRollGuarantee?>.Bad($"Ошибка при получении информации: {ex.Message}");
        }
        
        return result;
    }

    public async Task<OperationResult<RollCountResponse>> ResetRollCountAsync(string twitchId)
    {
        var result = OperationResult<RollCountResponse>.Bad("Ошибка при сбросе счетчика роллов");
        
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

                var rollCountResponse = new RollCountResponse
                {
                    Success = true,
                    CurrentRollCount = 0,
                    Message = "Счетчик роллов успешно сброшен"
                };

                result = OperationResult<RollCountResponse>.Ok("Счетчик роллов сброшен", rollCountResponse);
            }
            else
            {
                result = OperationResult<RollCountResponse>.Bad("Пользователь не найден в системе гарантов");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при сбросе счетчика роллов для пользователя {TwitchId}",
                twitchId
            );
            result = OperationResult<RollCountResponse>.Bad($"Ошибка при сбросе счетчика: {ex.Message}");
        }
        
        return result;
    }

    public async Task<OperationResult<bool>> DeleteGuaranteeAsync(string twitchId)
    {
        var result = OperationResult<bool>.Bad("Ошибка при удалении пользователя из системы гарантов");
        
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var guarantee = await dbContext.WaifuRollGuarantees.FirstOrDefaultAsync(g =>
                g.TwitchId == twitchId
            );

            if (guarantee != null)
            {
                dbContext.WaifuRollGuarantees.Remove(guarantee);
                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Удален пользователь {TwitchId} из системы гарантов после выпадения VIP",
                    twitchId
                );
                
                result = OperationResult<bool>.Ok("Пользователь удален из системы гарантов", true);
            }
            else
            {
                result = OperationResult<bool>.Bad("Пользователь не найден в системе гарантов");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при удалении пользователя {TwitchId} из системы гарантов",
                twitchId
            );
            result = OperationResult<bool>.Bad($"Ошибка при удалении: {ex.Message}");
        }
        
        return result;
    }
}
