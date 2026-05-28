using System.Collections.Generic;
using System.Linq;
using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Сервис для работы с базой данных фоловеров
/// </summary>
public class FollowerDbService(
    IDbContextFactory<AppDbContext> factory,
    TwitchUserEnsureService ensureService,
    ILogger<FollowerDbService> logger
)
{
    /// <summary>
    /// Получить всех фоловеров из базы данных
    /// </summary>
    /// <returns>Список FollowerInfo</returns>
    public async Task<List<FollowerInfo>> GetAllFollowersFromDbAsync()
    {
        try
        {
            await using var context = await factory.CreateDbContextAsync();
            var dbEntities = await context
                .FollowersEntitys.Include(e => e.TwitchUser)
                .AsNoTracking()
                .ToListAsync();

            return [.. dbEntities];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении фоловеров из базы данных");
            return [];
        }
    }

    /// <summary>
    /// Получить информацию о конкретном фоловере из базы данных
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <returns>FollowerInfo или null</returns>
    public async Task<FollowerInfo?> GetFollowerFromDbAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        try
        {
            await using var context = await factory.CreateDbContextAsync();
            var dbEntity = await context
                .FollowersEntitys.Include(e => e.TwitchUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.UserId == userId);

            return dbEntity;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении фоловера {UserId} из базы данных", userId);
            return null;
        }
    }

    /// <summary>
    /// Сохранить или обновить информацию о фоловере в базе данных
    /// </summary>
    /// <param name="followerInfo">Информация о фоловере</param>
    /// <returns>True если операция успешна</returns>
    public async Task<bool> SaveOrUpdateFollowerAsync(FollowerInfo? followerInfo)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(followerInfo?.UserId))
        {
            try
            {
                // ВАЖНО: Сначала обеспечиваем наличие TwitchUser в БД
                if (followerInfo.TwitchUser != null)
                {
                    await ensureService.EnsureUserExistsAsync(followerInfo.TwitchUser);
                }
                else
                {
                    await ensureService.EnsureUserExistsAsync(followerInfo.UserId);
                }

                // Теперь работаем с FollowerInfo
                await using var context = await factory.CreateDbContextAsync();
                var existingEntity = await context
                    .FollowersEntitys.AsNoTracking()
                    .FirstOrDefaultAsync(f => f.UserId == followerInfo.UserId);

                // Обнуляем навигационное свойство, чтобы EF не пытался добавить TwitchUser
                followerInfo.TwitchUser = null;

                if (existingEntity != null)
                {
                    // Обновляем существующую запись
                    context.FollowersEntitys.Update(followerInfo);
                }
                else
                {
                    // Создаем новую запись
                    context.FollowersEntitys.Add(followerInfo);
                }

                await context.SaveChangesAsync();
                result = true;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка при сохранении фоловера {UserId} в базу данных",
                    followerInfo.UserId
                );
            }
        }

        return result;
    }

    /// <summary>
    /// Сохранить или обновить список фоловеров в базе данных
    /// </summary>
    /// <param name="followersInfo">Список информации о фоловерах</param>
    /// <returns>Количество сохраненных записей</returns>
    public async Task<int> SaveOrUpdateFollowersAsync(ICollection<FollowerInfo>? followersInfo)
    {
        var savedCount = 0;

        if (followersInfo is { Count: > 0 })
        {
            try
            {
                // Подготавливаем списки пользователей для обработки
                var userIds = followersInfo.Select(f => f.UserId).ToList();
                var usersWithoutTwitchUserEntity = followersInfo
                    .Where(e => e.TwitchUser == null)
                    .Select(e => e.UserId)
                    .Distinct()
                    .ToList();
                var usersWithEntity = followersInfo
                    .Where(e => e.TwitchUser != null)
                    .Select(e => e.TwitchUser!)
                    .ToList();

                // ВАЖНО: Сначала обеспечиваем наличие всех TwitchUser в БД
                // Это нужно сделать ДО создания контекста для работы с FollowerInfo
                await ensureService.EnsureUsersExistsAsync(usersWithoutTwitchUserEntity);
                foreach (var twitchUser in usersWithEntity)
                {
                    await ensureService.EnsureUserExistsAsync(twitchUser);
                }

                // Теперь работаем с FollowerInfo
                await using var context = await factory.CreateDbContextAsync();

                var existingEntities = await context
                    .FollowersEntitys.AsNoTracking()
                    .Where(f => userIds.Contains(f.UserId))
                    .ToListAsync();

                foreach (var followerInfo in followersInfo)
                {
                    var existingEntity = existingEntities.FirstOrDefault(e =>
                        e.UserId == followerInfo.UserId
                    );

                    // Обнуляем навигационное свойство, чтобы EF не пытался добавить TwitchUser
                    // Связь по UserId (FK) будет работать автоматически
                    followerInfo.TwitchUser = null;

                    if (existingEntity != null)
                    {
                        context.FollowersEntitys.Update(followerInfo);
                    }
                    else
                    {
                        context.FollowersEntitys.Add(followerInfo);
                    }
                }

                savedCount = await context.SaveChangesAsync();
                logger.LogInformation("Сохранено {Count} фоловеров в базу данных", savedCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при массовом сохранении фоловеров в базу данных");
            }
        }

        return savedCount;
    }

    /// <summary>
    /// Удалить фоловера из базы данных
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <returns>True если операция успешна</returns>
    public async Task<bool> DeleteFollowerAsync(string userId)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                await using var context = await factory.CreateDbContextAsync();
                var entity = await context
                    .FollowersEntitys.AsNoTracking()
                    .FirstOrDefaultAsync(f => f.UserId == userId);

                if (entity != null)
                {
                    context.FollowersEntitys.Remove(entity);
                    await context.SaveChangesAsync();
                    result = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при удалении фоловера {UserId} из базы данных", userId);
            }
        }

        return result;
    }

    /// <summary>
    /// Удалить несколько фоловеров из базы данных
    /// </summary>
    /// <param name="userIds">Список ID пользователей для удаления</param>
    /// <returns>Количество удаленных записей</returns>
    public async Task<int> DeleteFollowersAsync(ICollection<string> userIds)
    {
        var result = 0;

        if (userIds is { Count: > 0 })
        {
            try
            {
                await using var context = await factory.CreateDbContextAsync();
                var entities = await context
                    .FollowersEntitys.AsNoTracking()
                    .Where(f => userIds.Contains(f.UserId))
                    .ToListAsync();

                if (entities.Count > 0)
                {
                    context.FollowersEntitys.RemoveRange(entities);
                    result = await context.SaveChangesAsync();
                    logger.LogInformation("Удалено {Count} фоловеров из базы данных", result);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при массовом удалении фоловеров из базы данных");
            }
        }

        return result;
    }

    /// <summary>
    /// Получить количество фоловеров в базе данных
    /// </summary>
    /// <returns>Количество фоловеров</returns>
    public async Task<int> GetFollowersCountAsync()
    {
        try
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.FollowersEntitys.AsNoTracking().CountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подсчете фоловеров в базе данных");
            return 0;
        }
    }

    /// <summary>
    /// Очистить все данные о фоловерах из базы данных
    /// </summary>
    /// <returns>Количество удаленных записей</returns>
    public async Task<int> ClearAllFollowersAsync()
    {
        try
        {
            await using var context = await factory.CreateDbContextAsync();
            var count = await context.FollowersEntitys.CountAsync();
            context.FollowersEntitys.RemoveRange(context.FollowersEntitys);
            await context.SaveChangesAsync();

            logger.LogInformation("Удалено {Count} фоловеров из базы данных", count);
            return count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при очистке всех фоловеров из базы данных");
            return 0;
        }
    }

    /// <summary>
    /// Получить фоловеров, которые нужно обновить (старше указанного времени)
    /// </summary>
    /// <param name="olderThan">Обновить фоловеров старше этого времени</param>
    /// <returns>Список UserId для обновления</returns>
    public async Task<List<string>> GetFollowersToUpdateAsync(DateTime olderThan)
    {
        try
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context
                .FollowersEntitys.AsNoTracking()
                .Include(f => f.TwitchUser)
                .Where(f => f.TwitchUser != null && f.TwitchUser.LastUpdated < olderThan)
                .Select(f => f.UserId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении списка фоловеров для обновления");
            return [];
        }
    }

    /// <summary>
    /// Получить пользователей без аватарок из базы данных
    /// </summary>
    /// <returns>Список FollowerInfo пользователей без аватарок</returns>
    public async Task<List<FollowerInfo>> GetUsersWithoutAvatarsAsync()
    {
        try
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context
                .FollowersEntitys.AsNoTracking()
                .Include(f => f.TwitchUser)
                .Where(f =>
                    f.TwitchUser == null || string.IsNullOrWhiteSpace(f.TwitchUser.ProfileImageUrl)
                )
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении пользователей без аватарок из базы данных");
            return [];
        }
    }

    /// <summary>
    /// Получить количество пользователей без аватарок
    /// </summary>
    /// <returns>Количество пользователей без аватарок</returns>
    public async Task<int> GetUsersWithoutAvatarsCountAsync()
    {
        try
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context
                .FollowersEntitys.AsNoTracking()
                .Include(f => f.TwitchUser)
                .CountAsync(f =>
                    f.TwitchUser == null || string.IsNullOrWhiteSpace(f.TwitchUser.ProfileImageUrl)
                );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подсчете пользователей без аватарок в базе данных");
            return 0;
        }
    }
}
