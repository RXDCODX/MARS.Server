using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Services;

/// <summary>
/// Процессор ежедневных отметок Honkai: Star Rail
/// </summary>
public class HonkaiDailyMarkupProcessor : IHonkaiDailyMarkupProcessor
{
    private readonly IHonkaiUserRepository _userRepository;
    private readonly IHonkaiRewardService _rewardService;
    private readonly IHonkaiNotificationProvider _notificationProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<HonkaiDailyMarkupProcessor> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр процессора ежедневных отметок
    /// </summary>
    /// <param name="userRepository">Репозиторий пользователей</param>
    /// <param name="rewardService">Сервис наград</param>
    /// <param name="notificationProvider">Провайдер уведомлений</param>
    /// <param name="httpClientFactory">Фабрика HTTP клиентов</param>
    /// <param name="environment">Информация об окружении</param>
    /// <param name="logger">Логгер для записи событий</param>
    public HonkaiDailyMarkupProcessor(
        IHonkaiUserRepository userRepository,
        IHonkaiRewardService rewardService,
        IHonkaiNotificationProvider notificationProvider,
        IHttpClientFactory httpClientFactory,
        IHostEnvironment environment,
        ILogger<HonkaiDailyMarkupProcessor> logger)
    {
        _userRepository = userRepository;
        _rewardService = rewardService;
        _notificationProvider = notificationProvider;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Обрабатывает ежедневные отметки для всех пользователей
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат обработки отметок</returns>
    public async Task<DailyMarkupProcessingResult> ProcessDailyMarkupsAsync(CancellationToken cancellationToken = default)
    {
        var result = new DailyMarkupProcessingResult
        {
            ProcessingStartTime = DateTime.UtcNow
        };

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();

            _logger.LogInformation("Начинаем проверку и активацию ежедневных отметок");

            var users = await _userRepository.GetUsersNeedingDailyMarkupAsync(cancellationToken);
            result.TotalUsersToProcess = users.Count;

            if (users.Count == 0)
            {
                _logger.LogDebug("Нет пользователей, требующих отметки");
                result.ProcessingEndTime = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation("Найдено {Count} пользователей для отметки", users.Count);

            foreach (var user in users)
            {
                try
                {
                    var userResult = await ProcessUserMarkupAsync(user, httpClient, cancellationToken);
                    
                    if (userResult.Success)
                    {
                        result.SuccessfullyProcessed++;
                        
                        // Обновляем время последней отметки
                        await _userRepository.UpdateLastMarkupTimeAsync(user, cancellationToken);

                        _logger.LogInformation(
                            "Успешно активированы отметки для пользователя {UserId}",
                            user.Id
                        );
                    }
                    else
                    {
                        result.FailedToProcess++;
                        result.Errors.Add($"User {user.Id}: {userResult.ErrorMessage}");
                        
                        // Отправляем уведомление об ошибке в продакшене
                        if (user.TelegramId != null && _environment.IsProduction())
                        {
                            await _notificationProvider.SendMarkupFailureNotificationAsync(
                                user.TelegramId.Value,
                                user.Id,
                                cancellationToken
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.FailedToProcess++;
                    result.Errors.Add($"User {user.Id}: {ex.Message}");
                    
                    _logger.LogError(ex, "Error processing markup for user {UserId}", user.Id);
                }
            }

            _logger.LogInformation(
                "Завершена проверка ежедневных отметок для {Count} пользователей. Успешно: {Success}, Ошибок: {Failed}",
                users.Count,
                result.SuccessfullyProcessed,
                result.FailedToProcess
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during daily markup processing");
            result.Errors.Add($"General error: {ex.Message}");
        }
        finally
        {
            result.ProcessingEndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Обрабатывает ежедневные отметки для конкретного пользователя
    /// </summary>
    /// <param name="user">Пользователь для обработки</param>
    /// <param name="httpClient">HTTP клиент для запросов</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат обработки отметки для пользователя</returns>
    public async Task<UserMarkupResult> ProcessUserMarkupAsync(
        DailyAutoMarkupUser user, 
        HttpClient httpClient, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rewardResult = await _rewardService.ClaimDailyRewardAsync(user, httpClient, cancellationToken);
            
            if (rewardResult.Success && user.TelegramId != null)
            {
                // Отправляем уведомление об успехе с информацией о награде
                await _notificationProvider.SendMarkupSuccessNotificationAsync(
                    user.TelegramId.Value,
                    rewardResult.RewardName ?? "награда",
                    rewardResult.Amount ?? 1,
                    cancellationToken
                );
            }

            return new UserMarkupResult
            {
                Success = rewardResult.Success,
                User = user,
                RewardResult = rewardResult,
                ErrorMessage = rewardResult.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing markup for user {UserId}", user.Id);
            
            return new UserMarkupResult
            {
                Success = false,
                User = user,
                ErrorMessage = $"Ошибка при работе с Honkai API: {ex.Message}"
            };
        }
    }
}