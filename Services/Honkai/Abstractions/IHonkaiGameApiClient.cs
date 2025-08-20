using MarchSeven.Models.HonkaiStarRail.Entitys;
using MarchSeven.Models.HonkaiStarRail.StarRailDailyNote;
using MarchSeven.Models.HoYoLab;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Abstractions;

/// <summary>
/// Интерфейс для взаимодействия с API игры Honkai: Star Rail
/// </summary>
public interface IHonkaiGameApiClient
{
    /// <summary>
    /// Получает информацию о пользователе Star Rail
    /// </summary>
    /// <param name="user">Пользователь для получения информации</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Информация о пользователе Star Rail или null, если не найден</returns>
    Task<StarRailUser?> GetStarRailUserAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает ежедневные заметки (информацию об энергии и ресурсах)
    /// </summary>
    /// <param name="user">Пользователь для получения заметок</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Ежедневные заметки или null при ошибке</returns>
    Task<StarRailDailyNote?> GetDailyNoteAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает статистику пользователя
    /// </summary>
    /// <param name="user">Пользователь для получения статистики</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Статистика пользователя или null при ошибке</returns>
    Task<UserStatsData?> GetUserStatsAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default);
}