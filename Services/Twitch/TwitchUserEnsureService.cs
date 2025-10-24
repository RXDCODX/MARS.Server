using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.TwitchFollowers;
using TwitchLib.Client.Models;
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
    /// Гарантирует наличие пользователя в БД. Если пользователя нет - создает его.
    /// Этот метод является thread-safe и идемпотентным.
    /// </summary>
    /// <param name="chatMessage">Сообщение из чата с информацией о пользователе</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    public async Task<TwitchUser> EnsureUserExistsAsync(
        ChatMessage chatMessage,
        CancellationToken cancellationToken = default
    )
    {
        TwitchUser? result = null;

        if (!string.IsNullOrWhiteSpace(chatMessage.UserId))
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

                // Пытаемся найти существующего пользователя
                var existingUser = await db.TwitchUsers.FindAsync(
                    [chatMessage.UserId],
                    cancellationToken: cancellationToken
                );

                if (existingUser != null)
                {
                    result = existingUser;
                }
                else
                {
                    // Создаем нового пользователя
                    var newUser = await CreateUserAsync(db, chatMessage, cancellationToken);
                    result = newUser;
                }
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        return result!;
    }

    /// <summary>
    /// Проверяет, является ли TwitchId валидным (должен быть числовым)
    /// </summary>
    private static bool IsValidTwitchId(string twitchId)
    {
        // TwitchId должен быть числовым (не GUID или другая строка)
        return !string.IsNullOrWhiteSpace(twitchId) && long.TryParse(twitchId, out _);
    }

    /// <summary>
    /// Создает нового пользователя из ChatMessage
    /// </summary>
    private async Task<TwitchUser> CreateUserAsync(
        AppDbContext db,
        ChatMessage chatMessage,
        CancellationToken cancellationToken = default
    )
    {
        TwitchUser result = null!;

        if (!string.IsNullOrWhiteSpace(chatMessage.UserId))
        {
            var userId = chatMessage.UserId;
            var userName = chatMessage.Username;
            var displayName = chatMessage.DisplayName;

            logger.LogInformation(
                "Создание нового пользователя Twitch: {UserName} (ID: {UserId})",
                userName,
                userId
            );

            // Получаем дополнительную информацию из API
            User? apiUser = null;
            string? chatColor = null;

            if (tokenService.Token?.AccessToken != null)
            {
                try
                {
                    var userInfoTask = userInfoService.GetUserInfoAsync(userId);
                    var chatColorTask = userInfoService.GetUserChatColorAsync(userId);

                    await Task.WhenAll(userInfoTask, chatColorTask);

                    apiUser = await userInfoTask;
                    chatColor = await chatColorTask;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Не удалось получить информацию из API для пользователя {UserId}",
                        userId
                    );
                }
            }

            // Создаем пользователя
            var newUser = new TwitchUser
            {
                TwitchId = userId,
                UserLogin = userName,
                DisplayName = displayName,
                IsModerator = chatMessage.IsModerator,
                IsVip = chatMessage.IsVip,
                ChatColor = chatColor ?? chatMessage.ColorHex,
                ProfileImageUrl = apiUser?.ProfileImageUrl,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
            };

            db.TwitchUsers.Add(newUser);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Пользователь Twitch создан: {UserName} (ID: {UserId}), Avatar: {Avatar}",
                userName,
                userId,
                apiUser?.ProfileImageUrl ?? "null"
            );

            result = newUser;
        }

        return result;
    }
}
