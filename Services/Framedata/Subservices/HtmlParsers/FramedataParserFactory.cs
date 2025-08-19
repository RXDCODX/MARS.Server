using MARS.Server.Services.Framedata.Subservices.Entitys;

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
        return source switch
        {
            FramedataSource.Wavu => new WavuFramedataParser(
                logger,
                dbContextFactory,
                stagingService,
                cancellationToken,
                options
            ),

            FramedataSource.Tekkendocs => new TekkendocsFramedataParser(
                logger,
                dbContextFactory,
                stagingService,
                cancellationToken,
                options
            ),

            _ => throw new ArgumentException($"Неизвестный источник данных: {source}"),
        };
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
}
