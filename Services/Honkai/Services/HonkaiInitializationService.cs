using MARS.Server.Services.Honkai.Abstractions;
using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.Services.Honkai.Services;

/// <summary>
/// Сервис для инициализации данных Honkai при первом запуске приложения
/// </summary>
public class HonkaiInitializationService : IHonkaiInitializationService
{
    private readonly IHonkaiUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HonkaiInitializationService> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса инициализации Honkai
    /// </summary>
    /// <param name="userRepository">Репозиторий пользователей</param>
    /// <param name="configuration">Конфигурация приложения</param>
    /// <param name="logger">Логгер для записи событий</param>
    public HonkaiInitializationService(
        IHonkaiUserRepository userRepository,
        IConfiguration configuration,
        ILogger<HonkaiInitializationService> logger)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Выполняет полную инициализацию данных Honkai
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если инициализация прошла успешно</returns>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Начинаем инициализацию базы данных Honkai...");

            var hoyolabInitialized = await InitializeHoyolabDataAsync(cancellationToken);
            
            if (hoyolabInitialized)
            {
                _logger.LogInformation("Инициализация базы данных Honkai завершена успешно");
                return true;
            }
            else
            {
                _logger.LogWarning("Инициализация Hoyolab данных не выполнена");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации базы данных Honkai");
            return false;
        }
    }

    /// <summary>
    /// Инициализирует данные Hoyolab из конфигурации
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если инициализация прошла успешно</returns>
    public async Task<bool> InitializeHoyolabDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Инициализация данных Hoyolab...");

            // Проверяем, есть ли уже данные в таблице
            var existingUsers = await _userRepository.AnyUsersExistAsync(cancellationToken);
            if (existingUsers)
            {
                _logger.LogInformation(
                    "Данные Hoyolab уже существуют в базе данных, пропускаем инициализацию"
                );
                return true;
            }

            // Получаем конфигурацию Hoyolab
            var hoyolabConfig = _configuration
                .GetSection(AppBase.Base)
                .GetSection(HoyolabConfiguration.Section)
                .Get<HoyolabConfiguration>();

            if (hoyolabConfig == null)
            {
                _logger.LogWarning("Конфигурация Hoyolab не найдена, пропускаем инициализацию");
                return false;
            }

            // Создаем пользователя по умолчанию из конфигурации
            var defaultUser = new DailyAutoMarkupUser
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                LtmidV2 = hoyolabConfig.Ltmid_v2,
                LTokenV2 = hoyolabConfig.Ltoken_v2,
                LtuidV2 = hoyolabConfig.Ltuid_v2,
                TelegramId = TelegramExstension.Rxdcodx,
                LastAutoMarkup = DateTime.UtcNow.AddDays(-1), // Устанавливаем вчерашнюю дату для первой проверки
            };

            // Добавляем пользователя в базу данных
            await _userRepository.CreateUserAsync(defaultUser, cancellationToken);

            _logger.LogInformation(
                "Данные Hoyolab успешно инициализированы. Создан пользователь с ID: {UserId}",
                defaultUser.Id
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации данных Hoyolab");
            return false;
        }
    }
}