using MARS.Server.Services.Framedata.Entitys;

namespace MARS.Server.Services.Framedata.Subservices.Entitys;

/// <summary>
/// Интерфейс для парсеров фреймдаты Tekken 8
/// </summary>
public interface IFramedataParser
{
    /// <summary>
    /// Настройки парсинга
    /// </summary>
    FramedataParserOptions Options { get; }

    /// <summary>
    /// Парсит персонажей и их мувы
    /// </summary>
    /// <param name="characterNamesToParse">Список имен персонажей для парсинга (null для всех)</param>
    /// <returns>Список имен распарсенных персонажей</returns>
    Task<List<string>> ParseCharactersAndMoves(List<string>? characterNamesToParse = null);

    /// <summary>
    /// Парсит только персонажей без мувов
    /// </summary>
    /// <param name="characterNamesToParse">Список имен персонажей для парсинга (null для всех)</param>
    /// <returns>Список имен распарсенных персонажей</returns>
    Task<List<string>> ParseCharactersOnly(List<string>? characterNamesToParse = null);

    /// <summary>
    /// Получает мувлист для конкретного персонажа
    /// </summary>
    /// <param name="character">Персонаж</param>
    /// <returns>Список мувов</returns>
    Task<List<Move>> GetMoveList(TekkenCharacter character);
}

/// <summary>
/// Настройки парсера фреймдаты
/// </summary>
public class FramedataParserOptions
{
    /// <summary>
    /// Задержка между запросами в секундах
    /// </summary>
    public int RequestDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Задержка между персонажами в секундах
    /// </summary>
    public int CharacterDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Добавлять ли изменения в ожидающие через сервис
    /// </summary>
    public bool UseStagingService { get; set; } = true;

    /// <summary>
    /// Парсить ли мувы для персонажей
    /// </summary>
    public bool ParseMoves { get; set; } = true;

    /// <summary>
    /// Максимальное количество попыток для одного запроса
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Таймаут для HTTP запросов в секундах
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;
}
