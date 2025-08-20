using MarchSeven;
using MarchSeven.Models.Core;
using MarchSeven.Models.Core.Cookie;
using MarchSeven.Util.Errors;
using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Services;

/// <summary>
/// Сервис для работы с ежедневными наградами Honkai: Star Rail
/// </summary>
public class HonkaiRewardService : IHonkaiRewardService
{
    private readonly IHonkaiGameApiClient _gameApiClient;
    private readonly ILogger<HonkaiRewardService> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса наград Honkai
    /// </summary>
    /// <param name="gameApiClient">Клиент для взаимодействия с API игры</param>
    /// <param name="logger">Логгер для записи событий</param>
    public HonkaiRewardService(
        IHonkaiGameApiClient gameApiClient,
        ILogger<HonkaiRewardService> logger)
    {
        _gameApiClient = gameApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Получает ежедневную награду для пользователя
    /// </summary>
    /// <param name="user">Пользователь для получения награды</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат получения награды с информацией о награде</returns>
    public async Task<HonkaiRewardResult> ClaimDailyRewardAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Сначала проверяем, что у пользователя есть аккаунт
            var accountInfo = await _gameApiClient.GetUserStatsAsync(user, httpClient, cancellationToken);
            if (accountInfo?.GameLists == null)
            {
                return new HonkaiRewardResult
                {
                    Success = false,
                    ErrorMessage = "Не удалось получить информацию об аккаунте"
                };
            }

            // Создаем клиент для получения награды
            var client = CreateMarchSevenClient(user, httpClient);
            var response = await client.StarRail.ClaimDailyRewardAsync();

            _logger.LogInformation(
                "Daily reward claimed successfully for user {UserId}: {RewardName} x{Amount}",
                user.Id,
                response.RewardName,
                response.Amount
            );

            return new HonkaiRewardResult
            {
                Success = true,
                RewardName = response.RewardName,
                Amount = response.Amount
            };
        }
        catch (DailyRewardAlreadyReceivedException)
        {
            _logger.LogInformation("Daily reward already received for user {UserId}", user.Id);
            return new HonkaiRewardResult
            {
                Success = false,
                ErrorMessage = "Награда уже получена сегодня"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming daily reward for user {UserId}", user.Id);
            return new HonkaiRewardResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
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