using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TwitchUsersController(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<TwitchUsersController> logger
) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperationResult<List<TwitchUserDto>>>> GetAllUsers(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<TwitchUserDto>>> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var users = await db
                .TwitchUsers.AsNoTracking()
                .OrderBy(u => u.DisplayName)
                .Select(u => new TwitchUserDto
                {
                    TwitchId = u.TwitchId,
                    UserLogin = u.UserLogin,
                    DisplayName = u.DisplayName,
                    ProfileImageUrl = u.ProfileImageUrl,
                    ChatColor = u.ChatColor,
                    IsModerator = u.IsModerator,
                    IsVip = u.IsVip,
                    IsBroadcaster = u.TwitchId == TwitchExstension.ChannelId,
                    IsInBlockList = u.IsInBlockList,
                    AliasNickname = u.AliasNickname,
                    FollowedAt = u.FollowedAt,
                    LastUpdated = u.LastUpdated,
                    CreatedAt = u.CreatedAt,
                })
                .ToListAsync(cancellationToken);

            result = Ok(
                OperationResult<List<TwitchUserDto>>.Ok("Получены все пользователи Twitch", users)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Twitch users");
            result = Ok(
                OperationResult<List<TwitchUserDto>>.Bad(
                    "Ошибка при получении пользователей Twitch",
                    []
                )
            );
        }

        return result;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OperationResult<TwitchUserDto?>>> GetUser(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<TwitchUserDto?>> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var user = await db
                .TwitchUsers.AsNoTracking()
                .Where(u => u.TwitchId == id)
                .Select(u => new TwitchUserDto
                {
                    TwitchId = u.TwitchId,
                    UserLogin = u.UserLogin,
                    DisplayName = u.DisplayName,
                    ProfileImageUrl = u.ProfileImageUrl,
                    ChatColor = u.ChatColor,
                    IsModerator = u.IsModerator,
                    IsVip = u.IsVip,
                    IsBroadcaster = u.TwitchId == TwitchExstension.ChannelId,
                    IsInBlockList = u.IsInBlockList,
                    AliasNickname = u.AliasNickname,
                    FollowedAt = u.FollowedAt,
                    LastUpdated = u.LastUpdated,
                    CreatedAt = u.CreatedAt,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user != null)
            {
                result = Ok(OperationResult<TwitchUserDto?>.Ok("Пользователь найден", user));
            }
            else
            {
                result = Ok(
                    OperationResult<TwitchUserDto?>.Bad($"Пользователь с ID {id} не найден", null)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Twitch user with ID: {Id}", id);
            result = Ok(
                OperationResult<TwitchUserDto?>.Bad("Ошибка при получении пользователя", null)
            );
        }

        return result;
    }

    [HttpPost]
    public async Task<ActionResult<OperationResult<TwitchUserDto?>>> CreateUser(
        CreateTwitchUserRequest? request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<TwitchUserDto?>> result;
        try
        {
            if (
                request == null
                || string.IsNullOrWhiteSpace(request.TwitchId)
                || string.IsNullOrWhiteSpace(request.UserLogin)
                || string.IsNullOrWhiteSpace(request.DisplayName)
            )
            {
                result = Ok(
                    OperationResult<TwitchUserDto?>.Bad(
                        "TwitchId, UserLogin и DisplayName обязательны",
                        null
                    )
                );
                return result;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var exists = await db
                .TwitchUsers.AsNoTracking()
                .AnyAsync(u => u.TwitchId == request.TwitchId, cancellationToken);

            if (exists)
            {
                result = Ok(
                    OperationResult<TwitchUserDto?>.Bad(
                        $"Пользователь с ID {request.TwitchId} уже существует",
                        null
                    )
                );
                return result;
            }

            var user = new TwitchUser
            {
                TwitchId = request.TwitchId,
                UserLogin = request.UserLogin,
                DisplayName = request.DisplayName,
                ProfileImageUrl = request.ProfileImageUrl,
                ChatColor = request.ChatColor,
                IsModerator = request.IsModerator,
                IsVip = request.IsVip,
                IsInBlockList = request.IsInBlockList,
                AliasNickname = request.AliasNickname,
                CreatedAt = DateTime.Now,
                LastUpdated = DateTime.Now,
            };

            db.TwitchUsers.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            var dto = new TwitchUserDto
            {
                TwitchId = user.TwitchId,
                UserLogin = user.UserLogin,
                DisplayName = user.DisplayName,
                ProfileImageUrl = user.ProfileImageUrl,
                ChatColor = user.ChatColor,
                IsModerator = user.IsModerator,
                IsVip = user.IsVip,
                IsBroadcaster = user.TwitchId == TwitchExstension.ChannelId,
                IsInBlockList = user.IsInBlockList,
                AliasNickname = user.AliasNickname,
                FollowedAt = user.FollowedAt,
                LastUpdated = user.LastUpdated,
                CreatedAt = user.CreatedAt,
            };

            result = Ok(OperationResult<TwitchUserDto?>.Ok("Пользователь успешно создан", dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Twitch user");
            result = Ok(
                OperationResult<TwitchUserDto?>.Bad("Ошибка при создании пользователя", null)
            );
        }

        return result;
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<OperationResult<TwitchUserDto?>>> UpdateUser(
        string id,
        UpdateTwitchUserRequest? request,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<TwitchUserDto?>> result;
        try
        {
            if (request == null)
            {
                result = Ok(
                    OperationResult<TwitchUserDto?>.Bad("Тело запроса не может быть пустым", null)
                );
                return result;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var user = await db.TwitchUsers.FirstOrDefaultAsync(
                u => u.TwitchId == id,
                cancellationToken
            );

            if (user == null)
            {
                result = Ok(
                    OperationResult<TwitchUserDto?>.Bad($"Пользователь с ID {id} не найден", null)
                );
                return result;
            }

            if (request.UserLogin != null)
                user.UserLogin = request.UserLogin;
            if (request.DisplayName != null)
                user.DisplayName = request.DisplayName;
            if (request.ProfileImageUrl != null)
                user.ProfileImageUrl = request.ProfileImageUrl;
            if (request.ChatColor != null)
                user.ChatColor = request.ChatColor;
            if (request.IsModerator.HasValue)
                user.IsModerator = request.IsModerator.Value;
            if (request.IsVip.HasValue)
                user.IsVip = request.IsVip.Value;
            if (request.IsInBlockList.HasValue)
                user.IsInBlockList = request.IsInBlockList.Value;
            if (request.AliasNickname != null)
                user.AliasNickname = request.AliasNickname;

            user.LastUpdated = DateTime.Now;
            await db.SaveChangesAsync(cancellationToken);

            var dto = new TwitchUserDto
            {
                TwitchId = user.TwitchId,
                UserLogin = user.UserLogin,
                DisplayName = user.DisplayName,
                ProfileImageUrl = user.ProfileImageUrl,
                ChatColor = user.ChatColor,
                IsModerator = user.IsModerator,
                IsVip = user.IsVip,
                IsBroadcaster = user.TwitchId == TwitchExstension.ChannelId,
                IsInBlockList = user.IsInBlockList,
                AliasNickname = user.AliasNickname,
                FollowedAt = user.FollowedAt,
                LastUpdated = user.LastUpdated,
                CreatedAt = user.CreatedAt,
            };

            result = Ok(OperationResult<TwitchUserDto?>.Ok("Пользователь успешно обновлен", dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating Twitch user with ID: {Id}", id);
            result = Ok(
                OperationResult<TwitchUserDto?>.Bad("Ошибка при обновлении пользователя", null)
            );
        }

        return result;
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<OperationResult>> DeleteUser(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var user = await db.TwitchUsers.FirstOrDefaultAsync(
                u => u.TwitchId == id,
                cancellationToken
            );

            if (user == null)
            {
                result = Ok(OperationResult.Bad($"Пользователь с ID {id} не найден"));
                return result;
            }

            db.TwitchUsers.Remove(user);
            await db.SaveChangesAsync(cancellationToken);

            result = Ok(OperationResult.Ok("Пользователь успешно удален"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Twitch user with ID: {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении пользователя"));
        }

        return result;
    }
}
