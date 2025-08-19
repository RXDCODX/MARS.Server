using MARS.Server.Services.Honkai.Entitys;

namespace MARS.Server.DataBaseContext;

/// <summary>
/// Класс для инициализации данных в базе данных
/// </summary>
public class DataBaseInitializer(
    IDbContextFactory<AppDbContext> factory,
    IConfiguration configuration,
    ILogger<DataBaseInitializer> logger
)
{
    /// <summary>
    /// Выполняет инициализацию всех данных
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            logger.LogInformation("Начинаем инициализацию базы данных...");

            await InitializeHoyolabDataAsync();

            logger.LogInformation("Инициализация базы данных завершена успешно");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при инициализации базы данных");
            throw;
        }
    }

    /// <summary>
    /// Инициализирует данные Hoyolab из конфигурации
    /// </summary>
    private async Task InitializeHoyolabDataAsync()
    {
        try
        {
            logger.LogInformation("Инициализация данных Hoyolab...");

            // Проверяем, есть ли уже данные в таблице
            await using var context = await factory.CreateDbContextAsync();
            var existingUsers = await context.HonkaiMarkupUser.AsNoTracking().AnyAsync();

            if (existingUsers)
            {
                logger.LogInformation(
                    "Данные Hoyolab уже существуют в базе данных, пропускаем инициализацию"
                );
                return;
            }

            // Получаем конфигурацию Hoyolab
            var hoyolabConfig = configuration
                .GetSection(AppBase.Base)
                .GetSection(HoyolabConfiguration.Section)
                .Get<HoyolabConfiguration>();

            if (hoyolabConfig == null)
            {
                logger.LogWarning("Конфигурация Hoyolab не найдена, пропускаем инициализацию");
                return;
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
            await context.HonkaiMarkupUser.AddAsync(defaultUser);
            await context.SaveChangesAsync();

            logger.LogInformation(
                "Данные Hoyolab успешно инициализированы. Создан пользователь с ID: {UserId}",
                defaultUser.Id
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при инициализации данных Hoyolab");
            throw;
        }
    }
}
