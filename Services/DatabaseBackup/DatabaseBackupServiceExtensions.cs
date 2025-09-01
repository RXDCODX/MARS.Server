namespace MARS.Server.Services.DatabaseBackup;

/// <summary>
/// Расширения для регистрации сервиса резервного копирования
/// </summary>
public static class DatabaseBackupServiceExtensions
{
    /// <summary>
    /// Добавляет сервис резервного копирования в коллекцию сервисов
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Коллекция сервисов с добавленным сервисом резервного копирования</returns>
    public static IServiceCollection AddDatabaseBackupService(this IServiceCollection services)
    {
        services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
        return services;
    }
}
