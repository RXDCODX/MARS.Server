using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

/// <summary>
/// Сервис для работы с базой данных фоловеров
/// </summary>
public class FollowerDbService(
    IDbContextFactory<AppDbContext> factory,
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
            var dbEntities = await context.FollowersEntitys.AsNoTracking().ToListAsync();

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
                .FollowersEntitys.AsNoTracking()
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
        if (followerInfo == null || string.IsNullOrWhiteSpace(followerInfo.UserId))
        {
            return false;
        }

        try
        {
            await using var context = await factory.CreateDbContextAsync();
            var existingEntity = await context
                .FollowersEntitys.AsNoTracking()
                .FirstOrDefaultAsync(f => f.UserId == followerInfo.UserId);

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
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при сохранении фоловера {UserId} в базу данных",
                followerInfo.UserId
            );
            return false;
        }
    }

    /// <summary>
    /// Сохранить или обновить список фоловеров в базе данных
    /// </summary>
    /// <param name="followersInfo">Список информации о фоловерах</param>
    /// <returns>Количество сохраненных записей</returns>
    public async Task<int> SaveOrUpdateFollowersAsync(ICollection<FollowerInfo>? followersInfo)
    {
        if (followersInfo is not { Count: > 0 })
        {
            return 0;
        }

        var savedCount = 0;

        try
        {
            await using var context = await factory.CreateDbContextAsync();
            var userIds = followersInfo.Select(f => f.UserId).ToList();
            var existingEntities = await context
                .FollowersEntitys.AsNoTracking()
                .Where(f => userIds.Contains(f.UserId))
                .ToListAsync();

            foreach (var followerInfo in followersInfo)
            {
                var existingEntity = existingEntities.FirstOrDefault(e =>
                    e.UserId == followerInfo.UserId
                );

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

        return savedCount;
    }

    /// <summary>
    /// Удалить фоловера из базы данных
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <returns>True если операция успешна</returns>
    public async Task<bool> DeleteFollowerAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

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
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении фоловера {UserId} из базы данных", userId);
            return false;
        }
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
                .Where(f => f.LastUpdated < olderThan)
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
                .Where(f => string.IsNullOrWhiteSpace(f.ProfileImageUrl))
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
                .CountAsync(f => string.IsNullOrWhiteSpace(f.ProfileImageUrl));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при подсчете пользователей без аватарок в базе данных");
            return 0;
        }
    }

    /// <summary>
    /// Обновить аватарки для пользователей в базе данных
    /// </summary>
    /// <param name="followersInfo">Список информации о фоловерах с обновленными аватарками</param>
    /// <returns>Количество обновленных записей</returns>
    public async Task<int> UpdateAvatarsAsync(ICollection<FollowerInfo> followersInfo)
    {
        if (followersInfo is not { Count: > 0 })
        {
            return 0;
        }

        var updatedCount = 0;

        try
        {
            await using var context = await factory.CreateDbContextAsync();

            // Получаем только пользователей с обновленными аватарками
            var followersWithAvatars = followersInfo
                .Where(f => !string.IsNullOrWhiteSpace(f.ProfileImageUrl))
                .ToList();

            if (followersWithAvatars.Count == 0)
            {
                logger.LogWarning("Нет пользователей с аватарками для обновления в БД");
                return 0;
            }

            var userIds = followersWithAvatars.Select(f => f.UserId).ToList();
            var existingEntities = await context
                .FollowersEntitys.AsNoTracking()
                .Where(f => userIds.Contains(f.UserId))
                .ToListAsync();

            logger.LogDebug(
                "Найдено {Count} существующих записей в БД для обновления аватарок",
                existingEntities.Count
            );

            foreach (var followerInfo in followersWithAvatars)
            {
                var existingEntity = existingEntities.FirstOrDefault(e =>
                    e.UserId == followerInfo.UserId
                );

                if (existingEntity != null)
                {
                    // Обновляем аватарку независимо от того, была ли она пустой
                    existingEntity.ProfileImageUrl = followerInfo.ProfileImageUrl;
                    existingEntity.LastUpdated = DateTime.UtcNow;
                    context.FollowersEntitys.Update(existingEntity);
                    updatedCount++;

                    logger.LogDebug(
                        "Подготовлено обновление аватарки для пользователя {UserId}: {AvatarUrl}",
                        followerInfo.UserId,
                        followerInfo.ProfileImageUrl
                    );
                }
                else
                {
                    logger.LogWarning(
                        "Не найдена запись в БД для пользователя {UserId}",
                        followerInfo.UserId
                    );
                }
            }

            if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
                logger.LogInformation("Обновлено {Count} аватарок в базе данных", updatedCount);
            }
            else
            {
                logger.LogWarning("Не было обновлено ни одной записи в БД");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении аватарок в базе данных");
        }

        return updatedCount;
    }

    /// <summary>
    /// Обновить аватарку для конкретного пользователя в базе данных
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="profileImageUrl">URL аватарки</param>
    /// <returns>True если операция успешна</returns>
    public async Task<bool> UpdateUserAvatarAsync(string userId, string profileImageUrl)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(profileImageUrl))
        {
            return false;
        }

        try
        {
            await using var context = await factory.CreateDbContextAsync();
            var entity = await context
                .FollowersEntitys.AsNoTracking()
                .FirstOrDefaultAsync(f => f.UserId == userId);

            if (entity != null)
            {
                entity.ProfileImageUrl = profileImageUrl;
                entity.LastUpdated = DateTime.UtcNow;
                context.FollowersEntitys.Update(entity);
                await context.SaveChangesAsync();

                logger.LogDebug(
                    "Обновлена аватарка для пользователя {UserId} в базе данных",
                    userId
                );
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обновлении аватарки пользователя {UserId} в базе данных",
                userId
            );
            return false;
        }
    }
}
