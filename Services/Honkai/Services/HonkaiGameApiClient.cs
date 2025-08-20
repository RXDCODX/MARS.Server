using MarchSeven;
using MarchSeven.Models.Core;
using MarchSeven.Models.Core.Cookie;
using MarchSeven.Models.HonkaiStarRail.Entitys;
using MarchSeven.Models.HonkaiStarRail.StarRailDailyNote;
using MarchSeven.Models.HoYoLab;
using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Services;

/// <summary>
/// Клиент для взаимодействия с API игры Honkai: Star Rail через библиотеку MarchSeven
/// </summary>
public class HonkaiGameApiClient : IHonkaiGameApiClient
{
    private readonly ILogger<HonkaiGameApiClient> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр клиента API игры Honkai
    /// </summary>
    /// <param name="logger">Логгер для записи событий</param>
    public HonkaiGameApiClient(ILogger<HonkaiGameApiClient> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Получает информацию о пользователе Star Rail
    /// </summary>
    /// <param name="user">Пользователь для получения информации</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Информация о пользователе Star Rail или null, если не найден</returns>
    public async Task<StarRailUser?> GetStarRailUserAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateMarchSevenClient(user, httpClient);
            var gameRoles = await client.GetGameRoles();

            var starRailRole = gameRoles.Data?.List?.FirstOrDefault(r =>
                r.GameRegionName == "hkrpg_global"
            );

            if (starRailRole == null)
            {
                _logger.LogDebug("Star Rail role not found for user {UserId}", user.Id);
                return null;
            }

            var hsrUser = new StarRailUser(int.Parse(starRailRole.GameUid));
            _logger.LogDebug("UID: {Uid}, Server: {Server}", hsrUser.Uid, hsrUser.Server);

            return hsrUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Star Rail user for user {UserId}", user.Id);
            return null;
        }
    }

    /// <summary>
    /// Получает ежедневные заметки (информацию об энергии и ресурсах)
    /// </summary>
    /// <param name="user">Пользователь для получения заметок</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Ежедневные заметки или null при ошибке</returns>
    public async Task<StarRailDailyNote?> GetDailyNoteAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var starRailUser = await GetStarRailUserAsync(user, httpClient, cancellationToken);
            if (starRailUser == null)
            {
                return null;
            }

            var client = CreateMarchSevenClient(user, httpClient);
            var dailyNote = await client.StarRail.FetchDailyNoteAsync(starRailUser);

            return dailyNote;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily note for user {UserId}", user.Id);
            return null;
        }
    }

    /// <summary>
    /// Получает статистику пользователя
    /// </summary>
    /// <param name="user">Пользователь для получения статистики</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Статистика пользователя или null при ошибке</returns>
    public async Task<UserStatsData?> GetUserStatsAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateMarchSevenClient(user, httpClient);
            var accountInfo = await client.StarRail.FetchUserStatsAsync();

            if (accountInfo?.Data?.GameLists == null)
            {
                _logger.LogWarning("Failed to get account info for user {UserId}", user.Id);
                return null;
            }

            return accountInfo.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user stats for user {UserId}", user.Id);
            return null;
        }
    }

    /// <summary>
    /// Создает клиент MarchSeven для взаимодействия с API
    /// </summary>
    /// <param name="user">Пользователь с данными аутентификации</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <returns>Настроенный клиент MarchSeven</returns>
    private static MarchSevenClient CreateMarchSevenClient(
        DailyAutoMarkupUser user,
        HttpClient httpClient)
    {
        var cookieV2 = new CookieV2
        {
            LTokenV2 = user.LTokenV2,
            LtMidV2 = user.LtmidV2,
            LtUidV2 = user.LtuidV2,
        };

        var clientData = new ClientData { HttpClient = httpClient, Language = "ru-RU" };

        return MarchSevenClient.Create(cookieV2, clientData);
    }
}