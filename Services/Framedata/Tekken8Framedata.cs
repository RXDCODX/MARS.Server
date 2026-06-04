using MARS.Server.Services.Framedata.Entitys.Enums;
using MARS.Server.Services.Framedata.Subservices.Entitys;
using MARS.Server.Services.Framedata.Subservices.HtmlParsers;
using MARS.Server.Services.Telegram;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MARS.Server.Services.Framedata;

public partial class Tekken8FrameData(
    ILogger<Tekken8FrameData> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime lifetime,
    ITelegramBotClient client,
    FramedataStagingService stagingService,
    IOptions<FramedataConfiguration> framedataOptions
) : BackgroundService, ITelegramusService
{
    private readonly FramedataConfiguration _framedataConfig = framedataOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await UpdateMovesForVictorina();
    }

    public async Task StartScrupFrameData(
        Chat? chat = null,
        FramedataParserOptions? options = null,
        FramedataSource? source = FramedataSource.Okizeme
    )
    {
        // Используем только Okizeme
        var primary = FramedataSource.Okizeme;

        try
        {
            // Создаем парсер для основного источника
            var primaryParser =
                options == null
                    ? FramedataParserFactory.CreateDefaultParser(
                        primary,
                        logger,
                        dbContextFactory,
                        stagingService,
                        _cancellationToken
                    )
                    : FramedataParserFactory.CreateParser(
                        primary,
                        logger,
                        dbContextFactory,
                        stagingService,
                        _cancellationToken,
                        options
                    );

            await primaryParser.ParseCharactersAndMoves();
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        try
        {
            // Вторичный источник всегда работает в режиме дополнения и обрабатывает ВСЕХ персонажей
            //var secondaryOptions = new FramedataParserOptions
            //{
            //    RequestDelaySeconds = options?.RequestDelaySeconds ?? 2,
            //    CharacterDelaySeconds = options?.CharacterDelaySeconds ?? 5,
            //    UseStagingService = options?.UseStagingService ?? true,
            //    ParseMoves = options?.ParseMoves ?? true,
            //    IsSupplementMode = true, // Всегда включаем режим дополнения
            //    MaxRetries = options?.MaxRetries ?? 3,
            //    HttpTimeoutSeconds = options?.HttpTimeoutSeconds ?? 30,
            //};

            //var secondaryParser = FramedataParserFactory.CreateParser(
            //    secondary,
            //    logger,
            //    dbContextFactory,
            //    stagingService,
            //    _cancellationToken,
            //    secondaryOptions
            //);

            //// Обрабатываем ВСЕХ персонажей в режиме дополнения
            //var allCharacterKeys = Aliases.CharacterNameAliases.Keys.ToList();
            //await secondaryParser.ParseCharactersAndMoves(allCharacterKeys);
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        await UpdateMovesForVictorina();
        await client.SendMessage(
            chat switch
            {
                not null => chat,
                _ => TelegramExstension.Rxdcodx,
            },
            $"Парсинг теккен фрейм даты из {primary} завершён!",
            cancellationToken: _cancellationToken
        );
    }

    /// <summary>
    /// Парсит только персонажей без мувов
    /// </summary>
    /// <param name="source">Источник данных</param>
    /// <param name="useStagingService">Использовать ли сервис ожидающих изменений</param>
    /// <returns>Список имен распарсенных персонажей</returns>
    public async Task ParseCharactersOnly(FramedataSource source, bool useStagingService = true)
    {
        var options = new FramedataParserOptions
        {
            UseStagingService = useStagingService,
            ParseMoves = false,
            RequestDelaySeconds = 2,
            CharacterDelaySeconds = 5,
            MaxRetries = 3,
            HttpTimeoutSeconds = 30,
        };

        await Task.Factory.StartNew(
            async () =>
            {
                await StartScrupFrameData(null, options, source);
            },
            _cancellationToken
        );
    }

    /// <summary>
    /// Парсит персонажей и мувы с настраиваемыми параметрами
    /// </summary>
    /// <param name="source">Источник данных</param>
    /// <param name="options">Настройки парсера</param>
    /// <returns>Список имен распарсенных персонажей</returns>
    public async Task ParseWithCustomOptions(FramedataSource source, FramedataParserOptions options)
    {
        await Task.Factory.StartNew(
            async () =>
            {
                await StartScrupFrameData(null, options, source);
            },
            _cancellationToken
        );
    }

    /// <summary>
    /// Запускает дополнение фреймдаты с кастомными настройками
    /// </summary>
    /// <param name="source">Источник данных для дополнения</param>
    /// <param name="options">Настройки парсера</param>
    /// <param name="chat">Чат для уведомлений</param>
    public async Task SupplementWithCustomOptions(
        FramedataSource source,
        FramedataParserOptions options,
        Chat? chat = null
    )
    {
        await Task.Factory.StartNew(
            async () =>
            {
                await StartSupplementFrameData(chat, options, source);
            },
            _cancellationToken
        );
    }

    /// <summary>
    /// Запускает парсинг в режиме дополнения - вторичный источник заполняет только пустые поля
    /// </summary>
    /// <param name="chat">Чат для уведомлений</param>
    /// <param name="options">Настройки парсера</param>
    /// <param name="source">Источник данных для дополнения</param>
    public async Task StartSupplementFrameData(
        Chat? chat = null,
        FramedataParserOptions? options = null,
        FramedataSource? source = null
    )
    {
        var supplementSource = FramedataSource.Okizeme;

        try
        {
            // Создаем парсер для дополнения
            IFramedataParser supplementParser;

            if (options != null)
            {
                // Если переданы кастомные настройки, используем их, но обязательно устанавливаем режим дополнения
                options.IsSupplementMode = true;
                supplementParser = FramedataParserFactory.CreateParser(
                    supplementSource,
                    logger,
                    dbContextFactory,
                    stagingService,
                    _cancellationToken,
                    options
                );
            }
            else
            {
                // Используем готовый парсер в режиме дополнения
                supplementParser = FramedataParserFactory.CreateSupplementParser(
                    supplementSource,
                    logger,
                    dbContextFactory,
                    stagingService,
                    _cancellationToken
                );
            }

            // В режиме дополнения проходим всех персонажей обязательно
            var allCharacterKeys = Aliases.CharacterNameAliases.Keys.ToList();
            await supplementParser.ParseCharactersAndMoves(allCharacterKeys);

            await UpdateMovesForVictorina();

            await client.SendMessage(
                chat switch
                {
                    not null => chat,
                    _ => TelegramExstension.Rxdcodx,
                },
                $"Дополнение фреймдаты из {supplementSource} завершено!",
                cancellationToken: _cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            await client.SendMessage(
                chat switch
                {
                    not null => chat,
                    _ => TelegramExstension.Rxdcodx,
                },
                $"Ошибка при дополнении фреймдаты: {ex.Message}",
                cancellationToken: _cancellationToken
            );
        }
    }

    public async Task<(TekkenMoveTag Tag, Move[] Moves)?> GetMultipleMovesByTags(string input)
    {
        var split = input.Split(
            ' ',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );
        var lastSplit = split.Last();
        var isMultiple = lastSplit.EndsWith('s') || lastSplit.EndsWith("es");
        var characterName = string.Join(' ', split.SkipLast(1));

        var characterMovelist = await GetCharMoveListAsync(characterName);

        if (characterMovelist == null)
        {
            return null;
        }

        if (isMultiple)
        {
            var result = await GetMultipleMovesFromMovelistByTagAsync(lastSplit, characterMovelist);

            return result;
        }

        (TekkenMoveTag tag, Move? move) = await GetMoveFromMovelistByTagAsync(
            lastSplit,
            characterMovelist
        );

        return move != null ? (Tag: tag, [move]) : null;
    }

    public async Task<IDictionary<string, string>?> GetCharacterStances(
        string characterName,
        CancellationToken? stoppingToken
    )
    {
        stoppingToken ??= _cancellationToken;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            stoppingToken.Value
        );

        var tekkenChar = await FindCharacterInDatabaseAsync(characterName, dbContext);

        if (tekkenChar == null)
        {
            return null;
        }

        var movelist = await GetCharacterStances(tekkenChar, stoppingToken);
        return movelist;
    }

    private static readonly ValueComparer<Move> StancesComparer = new(
        (e1, e2) =>
            e1 != null
            && e2 != null
            && e1.StanceCode.Contains(e2.StanceCode, StringComparison.OrdinalIgnoreCase),
        e => HashCode.Combine(e.StanceCode)
    );

    public async Task<IDictionary<string, string>> GetCharacterStances(
        TekkenCharacter character,
        CancellationToken? stoppingToken
    )
    {
        stoppingToken ??= _cancellationToken;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            stoppingToken.Value
        );

        // Получаем только уникальные комбинации StanceCode и StanceName через группировку на уровне БД
        var stancesAndMoves = await dbContext
            .TekkenMoves.AsNoTracking()
            .Where(e =>
                e.CharacterName == character.Name
                && e.StanceName != null
                && e.StanceCode != string.Empty
            )
            .GroupBy(e => new { e.StanceCode, e.StanceName })
            .Select(g => new { g.Key.StanceCode, g.Key.StanceName })
            .ToDictionaryAsync(
                e => e.StanceCode,
                e => e.StanceName ?? string.Empty,
                stoppingToken.Value
            );

        return stancesAndMoves;
    }

    public async Task<TekkenCharacter?> GetTekkenCharacter(string name, bool isWithMoveList = false)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );
        TekkenCharacter result = null!;
        result = isWithMoveList
            ? await dbContext
                .TekkenCharacters.Include(e => e.Movelist)
                .AsNoTracking()
                .FirstAsync(e => e.Name.Equals(name), cancellationToken: _cancellationToken)
            : await dbContext
                .TekkenCharacters.AsNoTracking()
                .FirstAsync(e => e.Name.Equals(name), cancellationToken: _cancellationToken);

        return result;
    }

    public async Task<Move[]?> GetCharMoveListAsync(string charname)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );

        // Проверяем существование персонажа
        var characterExists = await dbContext
            .TekkenCharacters.AsNoTracking()
            .AnyAsync(e => e.Name.Equals(charname), cancellationToken: _cancellationToken);

        if (!characterExists)
        {
            return null;
        }

        // Загружаем только мувы без персонажа
        var moves = await dbContext
            .TekkenMoves.AsNoTracking()
            .Where(m => m.CharacterName.Equals(charname))
            .ToArrayAsync(_cancellationToken);

        return moves.Length > 0 ? moves : null;
    }

    public async Task<Move?> GetMoveAsync(string[]? command)
    {
        if (command == null || command.Length == 0)
        {
            return null;
        }

        var result = await FindCharacterByNameAsync(command);

        var charnameOut = result.character;
        var length = result.length;

        if (command.Length <= 1 || charnameOut is null)
        {
            return null;
        }

        var input = string.Join(" ", command.TakeLast(command.Length - length)).ToLower();

        if (string.IsNullOrWhiteSpace(charnameOut.Name) || string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        // Загружаем мувы без Include, Character будем подставлять вручную
        var movelist = await dbContext
            .TekkenMoves.AsNoTracking()
            .Where(e => e.CharacterName == charnameOut.Name)
            .ToListAsync(_cancellationToken);

        if (movelist is { Count: > 0 })
        {
            var move =
                await GetMoveFromMovelistByCommandAsync(input, movelist)
                ?? (await GetMoveFromMovelistByTagAsync(input, movelist)).move;

            // Подставляем персонажа вручную, чтобы избежать дополнительного запроса
            move?.Character = charnameOut;

            return move;
        }

        return null;
    }

    private async Task<(TekkenCharacter? character, int length)> FindCharacterByNameAsync(
        string[]? commandParts
    )
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );

        // Сначала пробуем найти по двум словам
        var charname = string.Join(" ", commandParts?.Take(2) ?? []);
        var character = await FindCharacterInDatabaseAsync(charname, dbContext);

        if (character != null)
        {
            return (character, 2);
        }

        // Если не нашли, пробуем по одному слову
        charname = string.Join(" ", commandParts?.Take(1) ?? []);
        character = await FindCharacterInDatabaseAsync(charname, dbContext);
        return (character, 1);
    }

    public async Task<TekkenCharacter?> FindCharacterInDatabaseAsync(
        string charname,
        AppDbContext dbContext
    )
    {
        foreach (var aliasPair in Aliases.CharacterNameAliases)
        {
            if (aliasPair.Key.Equals(charname) || aliasPair.Value.Any(e => e.Equals(charname)))
            {
                var characterName = aliasPair.Key;

                var characters = dbContext.TekkenCharacters.AsAsyncEnumerable();
                await foreach (TekkenCharacter tekkenCharacter in characters)
                {
                    if (
                        tekkenCharacter.Name.Equals(
                            characterName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        return tekkenCharacter;
                    }
                }
            }
        }
        return null;
    }

    private static Task<(TekkenMoveTag tag, Move[])?> GetMultipleMovesFromMovelistByTagAsync(
        string input,
        ICollection<Move> movelist
    )
    {
        Move[] moves = [];

        var typeWithoutStance = MoveTags
            .FirstOrDefault(e =>
                e.Value.Any(b => b.Equals(input, StringComparison.OrdinalIgnoreCase))
            )
            .Key;

        if (typeWithoutStance == TekkenMoveTag.None)
        {
            moves =
            [
                .. movelist.Where(e =>
                    (e.StanceName?.Equals(input) ?? false) || e.StanceCode.Equals(input)
                ),
            ];

            return Task.FromResult<(TekkenMoveTag tag, Move[])?>((TekkenMoveTag.None, moves));
        }

        switch (typeWithoutStance)
        {
            case TekkenMoveTag.HeatBurst:
                moves = [.. movelist.Where(e => e is { HeatBurst: true })];
                break;
            case TekkenMoveTag.HeatEngage:
                moves = [.. movelist.Where(e => e is { HeatEngage: true })];
                break;
            case TekkenMoveTag.HeatSmash:
                moves = [.. movelist.Where(e => e is { HeatSmash: true })];
                break;
            case TekkenMoveTag.Homing:
                moves = [.. movelist.Where(e => e is { Homing: true })];
                break;
            case TekkenMoveTag.PowerCrush:
                moves = [.. movelist.Where(e => e is { PowerCrush: true })];
                break;
            case TekkenMoveTag.Throw:
                moves = [.. movelist.Where(e => e is { Throw: true })];
                break;
            case TekkenMoveTag.Tornado:
                moves = [.. movelist.Where(e => e is { Tornado: true })];
                break;
        }

        return Task.FromResult<(TekkenMoveTag tag, Move[])?>((typeWithoutStance, moves));
    }

    private static Task<(TekkenMoveTag tag, Move? move)> GetMoveFromMovelistByTagAsync(
        string input,
        ICollection<Move> movelist
    )
    {
        Move? move = null;

        var typeWithoutStance = MoveTags
            .FirstOrDefault(e =>
                e.Value.Any(b => b.Equals(input, StringComparison.OrdinalIgnoreCase))
            )
            .Key;

        if (typeWithoutStance == TekkenMoveTag.None)
        {
            move = movelist.FirstOrDefault(e =>
                (e.StanceName?.Equals(input) ?? false) || e.StanceCode.Equals(input)
            );

            return Task.FromResult((TekkenMoveTag.None, move));
        }

        switch (typeWithoutStance)
        {
            case TekkenMoveTag.HeatBurst:
                move = movelist.LastOrDefault(e => e is { HeatBurst: true }, null);
                break;
            case TekkenMoveTag.HeatEngage:
                move = movelist.LastOrDefault(e => e is { HeatEngage: true }, null);
                break;
            case TekkenMoveTag.HeatSmash:
                move = movelist.LastOrDefault(e => e is { HeatSmash: true }, null);
                break;
            case TekkenMoveTag.Homing:
                move = movelist.LastOrDefault(e => e is { Homing: true }, null);
                break;
            case TekkenMoveTag.PowerCrush:
                move = movelist.LastOrDefault(e => e is { PowerCrush: true });
                break;
            case TekkenMoveTag.Throw:
                move = movelist.LastOrDefault(e => e is { Throw: true }, null);
                break;
            case TekkenMoveTag.Tornado:
                move = movelist.LastOrDefault(e => e is { Tornado: true }, null);
                break;
        }

        return Task.FromResult((TekkenMoveTag.None, move));
    }

    private static Task<Move?> GetMoveFromMovelistByCommandAsync(
        string movename,
        List<Move> movelist
    )
    {
        var replaced = ReplaceCommandCharacters(movename.ToLower());
        var currentMove = movelist.FirstOrDefault(
            move =>
                move != null && ReplaceCommandCharacters(move.Command.ToLower()).Equals(replaced),
            null
        );

        if (currentMove == null)
        {
            currentMove = movelist.FirstOrDefault(
                move =>
                    move != null
                    && ReplaceCommandCharacters(move.Command.ToLower()).StartsWith(replaced),
                null
            );

            if (currentMove == null)
            {
                currentMove = movelist.FirstOrDefault(
                    move =>
                        move != null
                        && ReplaceCommandCharacters(move.Command.ToLower()).Contains(replaced),
                    null
                );

                if (currentMove == null)
                {
                    return Task.FromResult<Move?>(null);
                }
            }
        }

        return Task.FromResult<Move?>(currentMove);
    }

    public async Task UpdateMovesForVictorina()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        // Используем батчинг для обработки мувов порциями
        const int batchSize = 1000;
        var list = new List<Move>();
        var totalProcessed = 0;

        while (true)
        {
            var batch = await dbContext
                .TekkenMoves.AsNoTracking()
                .Include(e => e.Character)
                .OrderBy(m => m.CharacterName)
                .ThenBy(m => m.Command)
                .Skip(totalProcessed)
                .Take(batchSize)
                .ToListAsync(_cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var move in batch)
            {
                if (int.TryParse(move.BlockFrame, out var frame))
                {
                    list.Add(move);
                }
                else if (move.BlockFrame?.Contains('~') ?? false)
                {
                    var split = move.BlockFrame.Split('~');
                    if (split.All(e => int.TryParse(e, out var _)))
                    {
                        list.Add(move);
                    }
                }
            }

            totalProcessed += batch.Count;

            // Если батч меньше размера - это последний батч
            if (batch.Count < batchSize)
            {
                break;
            }
        }

        VictorinaMoves = list;
    }
}
