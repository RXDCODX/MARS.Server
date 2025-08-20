namespace MARS.Server.Services.Honkai.Abstractions;

/// <summary>
/// Интерфейс для инициализации данных Honkai
/// </summary>
public interface IHonkaiInitializationService
{
    /// <summary>
    /// Выполняет полную инициализацию данных Honkai
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если инициализация прошла успешно</returns>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Инициализирует данные Hoyolab из конфигурации
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если инициализация прошла успешно</returns>
    Task<bool> InitializeHoyolabDataAsync(CancellationToken cancellationToken = default);
}