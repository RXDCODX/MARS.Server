using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.TwitchFollowers;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using User = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace MARS.Server.Services.Twitch;

/// <summary>
/// Сервис для гарантированного создания/получения пользователей Twitch в БД.
/// Использует паттерн GetOrCreate для обеспечения целостности данных
/// </summary>
public class TwitchUserEnsureService(
    IDbContextFactory<AppDbContext> dbFactory,
    TwitchUserInfoService userInfoService,
    TokenService tokenService,
    ILogger<TwitchUserEnsureService> logger
)
{
    /// <summary>
    /// Гарантирует наличие пользователя в БД из ChatMessage.
    /// </summary>
    /// <param name="chatMessage">Сообщение из чата с информацией о пользователе</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    public async Task<TwitchUser> EnsureUserExistsAsync(
        ChatMessage chatMessage,
        CancellationToken cancellationToken = default
    )
    {
        var twitchUser = TwitchUser.FromChatMessage(chatMessage);
        if (twitchUser == null)
        {
            throw new ArgumentException("Invalid ChatMessage data", nameof(chatMessage));
        }

        return await EnsureUserExistsAsync(twitchUser, cancellationToken);
    }

    /// <summary>
    /// Гарантирует наличие пользователя в БД по TwitchId и опциональным данным.
    /// Если пользователя нет - создает минимальную запись и пытается получить данные из API.
    /// </summary>
    /// <param name="twitchId">ID пользователя Twitch</param>
    /// <param name="userName">Логин пользователя (опционально)</param>
    /// <param name="displayName">Отображаемое имя (опционально)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    public async Task<TwitchUser> EnsureUserExistsAsync(
        string twitchId,
        string? userName = null,
        string? displayName = null,
        CancellationToken cancellationToken = default
    )
    {
        var twitchUser = TwitchUser.FromId(twitchId, userName, displayName);
        if (twitchUser == null)
        {
            throw new ArgumentException("Invalid TwitchId", nameof(twitchId));
        }

        return await EnsureUserExistsAsync(twitchUser, cancellationToken);
    }

    /// <summary>
    /// Обогащает данные пользователя информацией из Twitch API
    /// </summary>
    private async Task<TwitchUser> EnrichUserDataFromApiAsync(
        TwitchUser twitchUser,
        CancellationToken cancellationToken = default
    )
    {
        if (tokenService.Token?.AccessToken != null)
        {
            try
            {
                var userInfoTask = userInfoService.GetUserInfoAsync(twitchUser.TwitchId);
                var chatColorTask = userInfoService.GetUserChatColorAsync(twitchUser.TwitchId);

                await Task.WhenAll(userInfoTask, chatColorTask);

                var apiUser = await userInfoTask;
                var chatColor = await chatColorTask;

                // Обогащаем данные из API, если они не были установлены
                twitchUser.ProfileImageUrl ??= apiUser?.ProfileImageUrl;
                twitchUser.ChatColor ??= chatColor;

                if (apiUser != null)
                {
                    // Обновляем логин и имя из API, если они были дефолтными
                    if (twitchUser.UserLogin.StartsWith("user_"))
                    {
                        twitchUser.UserLogin = apiUser.Login ?? twitchUser.UserLogin;
                    }
                    if (twitchUser.DisplayName.StartsWith("User"))
                    {
                        twitchUser.DisplayName = apiUser.DisplayName ?? twitchUser.DisplayName;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Не удалось обогатить данные из API для пользователя {TwitchId}",
                    twitchUser.TwitchId
                );
            }
        }

        return twitchUser;
    }

    /// <summary>
    /// Гарантирует наличие пользователя в БД из OnMessageReceivedArgs.
    /// </summary>
    /// <param name="args">Аргументы события получения сообщения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    public async Task<TwitchUser> EnsureUserExistsAsync(
        OnMessageReceivedArgs args,
        CancellationToken cancellationToken = default
    )
    {
        var twitchUser = TwitchUser.FromOnMessageReceivedArgs(args);
        if (twitchUser == null)
        {
            throw new ArgumentException("Invalid OnMessageReceivedArgs data", nameof(args));
        }

        return await EnsureUserExistsAsync(twitchUser, cancellationToken);
    }

    /// <summary>
    /// Гарантирует наличие пользователя в БД из ChannelPointsCustomRewardRedemptionArgs.
    /// </summary>
    /// <param name="args">Аргументы события использования награды за баллы канала</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    public async Task<TwitchUser> EnsureUserExistsAsync(
        ChannelPointsCustomRewardRedemptionArgs args,
        CancellationToken cancellationToken = default
    )
    {
        var twitchUser = TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args);
        if (twitchUser == null)
        {
            throw new ArgumentException(
                "Invalid ChannelPointsCustomRewardRedemptionArgs data",
                nameof(args)
            );
        }

        return await EnsureUserExistsAsync(twitchUser, cancellationToken);
    }

    /// <summary>
    /// Гарантирует наличие пользователя в БД из готовой сущности TwitchUser.
    /// Если пользователь уже существует - обновляет его данные, иначе создает нового.
    /// </summary>
    /// <param name="twitchUser">Готовая сущность TwitchUser</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    public async Task<TwitchUser> EnsureUserExistsAsync(
        TwitchUser? twitchUser,
        CancellationToken cancellationToken = default
    )
    {
        TwitchUser? result = null!;

        if (twitchUser != null && !string.IsNullOrWhiteSpace(twitchUser.TwitchId))
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

                // Пытаемся найти существующего пользователя
                var existingUser = await db.TwitchUsers.FindAsync(
                    [twitchUser.TwitchId],
                    cancellationToken: cancellationToken
                );

                if (existingUser != null)
                {
                    // Обновляем существующего пользователя
                    existingUser.UserLogin = twitchUser.UserLogin;
                    existingUser.DisplayName = twitchUser.DisplayName;
                    existingUser.ProfileImageUrl =
                        twitchUser.ProfileImageUrl ?? existingUser.ProfileImageUrl;
                    existingUser.ChatColor = twitchUser.ChatColor ?? existingUser.ChatColor;
                    existingUser.IsModerator = twitchUser.IsModerator;
                    existingUser.IsVip = twitchUser.IsVip;
                    existingUser.LastUpdated = DateTime.UtcNow;

                    await db.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Обновлен пользователь Twitch: {UserName} (ID: {UserId})",
                        existingUser.UserLogin,
                        existingUser.TwitchId
                    );

                    result = existingUser;
                }
                else
                {
                    // Обогащаем данные из API перед созданием
                    var enrichedUser = await EnrichUserDataFromApiAsync(
                        twitchUser,
                        cancellationToken
                    );

                    logger.LogInformation(
                        "Создание нового пользователя Twitch: {UserName} (ID: {UserId})",
                        enrichedUser.UserLogin,
                        enrichedUser.TwitchId
                    );

                    db.TwitchUsers.Add(enrichedUser);
                    await db.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Создан новый пользователь Twitch: {UserName} (ID: {UserId}), Avatar: {Avatar}",
                        enrichedUser.UserLogin,
                        enrichedUser.TwitchId,
                        enrichedUser.ProfileImageUrl ?? "null"
                    );

                    result = enrichedUser;
                }
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        return result;
    }
}
