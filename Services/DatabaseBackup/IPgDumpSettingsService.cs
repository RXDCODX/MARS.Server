using MARS.Server.Services.DatabaseBackup.Entitys;

namespace MARS.Server.Services.DatabaseBackup;

/// <summary>
/// Сервис для управления настройками pg_dump
/// </summary>
public interface IPgDumpSettingsService
{
    /// <summary>
    /// Получает активные настройки pg_dump
    /// </summary>
    /// <returns>Активные настройки или null если не настроены</returns>
    Task<PgDumpSettings?> GetActiveSettingsAsync();

    /// <summary>
    /// Обновляет настройки pg_dump
    /// </summary>
    /// <param name="request">Запрос с новыми настройками</param>
    /// <returns>Обновленные настройки</returns>
    Task<PgDumpSettings> UpdateSettingsAsync(UpdatePgDumpSettingsRequest request);

    /// <summary>
    /// Валидирует путь к pg_dump
    /// </summary>
    /// <param name="pgDumpPath">Путь к pg_dump</param>
    /// <returns>Информация о валидации</returns>
    Task<PgDumpValidationInfo> ValidatePgDumpPathAsync(string pgDumpPath);

    /// <summary>
    /// Получает историю настроек pg_dump
    /// </summary>
    /// <returns>Список всех настроек</returns>
    Task<IEnumerable<PgDumpSettings>> GetSettingsHistoryAsync();

    /// <summary>
    /// Проверяет, настроены ли настройки pg_dump
    /// </summary>
    /// <returns>True если настройки существуют и валидны</returns>
    Task<bool> IsConfiguredAsync();
}
