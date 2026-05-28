using System.Threading;
using MARS.Server.Services.Framedata.Subservices.Entitys;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Framedata.Subservices.HtmlParsers;

/// <summary>
/// Фабрика для создания парсеров фреймдаты
/// </summary>
public static class FramedataParserFactory
{
    /// <summary>
    /// Создает парсер для указанного источника
    /// </summary>
    /// <param name="source">Источник данных</param>
    /// <param name="logger">Логгер</param>
    /// <param name="dbContextFactory">Фабрика контекста базы данных</param>
    /// <param name="stagingService">Сервис ожидающих изменений (может быть null)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <param name="options">Настройки парсера</param>
    /// <returns>Парсер фреймдаты</returns>
    public static IFramedataParser CreateParser(
        FramedataSource source,
        ILogger logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        FramedataStagingService? stagingService,
        CancellationToken cancellationToken,
        FramedataParserOptions? options = null
    )
    {
        if (source == FramedataSource.None)
        {
            throw new ArgumentException("Источник данных не задан");
        }

        return new OkizemeFramedataParser(
            logger,
            dbContextFactory,
            stagingService,
            cancellationToken,
            options
        );
    }

    /// <summary>
    /// Создает парсер с настройками по умолчанию
    /// </summary>
    /// <param name="source">Источник данных</param>
    /// <param name="logger">Логгер</param>
    /// <param name="dbContextFactory">Фабрика контекста базы данных</param>
    /// <param name="stagingService">Сервис ожидающих изменений (может быть null)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Парсер фреймдаты с настройками по умолчанию</returns>
    public static IFramedataParser CreateDefaultParser(
        FramedataSource source,
        ILogger logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        FramedataStagingService? stagingService,
        CancellationToken cancellationToken
    )
    {
        var options = new FramedataParserOptions
        {
            RequestDelaySeconds = 2,
            CharacterDelaySeconds = 5,
            UseStagingService = stagingService != null,
            ParseMoves = true,
            MaxRetries = 3,
            HttpTimeoutSeconds = 30,
        };

        return CreateParser(
            source,
            logger,
            dbContextFactory,
            stagingService,
            cancellationToken,
            options
        );
    }

    /// <summary>
    /// Создает парсер в режиме дополнения
    /// </summary>
    /// <param name="source">Источник данных</param>
    /// <param name="logger">Логгер</param>
    /// <param name="dbContextFactory">Фабрика контекста базы данных</param>
    /// <param name="stagingService">Сервис ожидающих изменений (может быть null)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Парсер фреймдаты в режиме дополнения</returns>
    public static IFramedataParser CreateSupplementParser(
        FramedataSource source,
        ILogger logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        FramedataStagingService? stagingService,
        CancellationToken cancellationToken
    )
    {
        var options = new FramedataParserOptions
        {
            RequestDelaySeconds = 2,
            CharacterDelaySeconds = 5,
            UseStagingService = stagingService != null,
            ParseMoves = true,
            IsSupplementMode = true,
            MaxRetries = 3,
            HttpTimeoutSeconds = 30,
        };

        return CreateParser(
            source,
            logger,
            dbContextFactory,
            stagingService,
            cancellationToken,
            options
        );
    }
}
