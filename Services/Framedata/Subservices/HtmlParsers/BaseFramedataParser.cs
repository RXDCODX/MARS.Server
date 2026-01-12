using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Subservices.Entitys;

namespace MARS.Server.Services.Framedata.Subservices.HtmlParsers;

/// <summary>
/// Базовый абстрактный класс для парсеров фреймдаты
/// </summary>
public abstract class BaseFramedataParser(
    ILogger logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    FramedataStagingService? stagingService,
    CancellationToken cancellationToken,
    FramedataParserOptions? options = null
) : IFramedataParser
{
    protected readonly ILogger Logger = logger;
    protected readonly IDbContextFactory<AppDbContext> DbContextFactory = dbContextFactory;
    protected readonly FramedataStagingService? StagingService = stagingService;
    protected readonly CancellationToken CancellationToken = cancellationToken;

    public FramedataParserOptions Options { get; } = options ?? new FramedataParserOptions();

    public abstract Task<List<string>> ParseCharactersAndMoves(
        List<string>? characterNamesToParse = null
    );
    public abstract Task<List<string>> ParseCharactersOnly(
        List<string>? characterNamesToParse = null
    );
    public abstract Task<List<Move>> GetMoveList(TekkenCharacter character);

    /// <summary>
    /// Сохраняет персонажа в базу данных
    /// </summary>
    protected async Task SaveCharacter(TekkenCharacter character)
    {
        if (Options.UseStagingService && StagingService != null)
        {
            // Через сервис ожидающих изменений
            await StagingService.StageCharacterAndMoves(
                character,
                [],
                options?.IsSupplementMode ?? false
            );
        }
        else
        {
            // Напрямую в базу данных
            await using var db = await DbContextFactory.CreateDbContextAsync(CancellationToken);

            var existing = await db.TekkenCharacters.FirstOrDefaultAsync(
                c => c.Name == character.Name,
                CancellationToken
            );

            if (existing == null)
            {
                await db.TekkenCharacters.AddAsync(character, CancellationToken);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(character);
            }

            await db.SaveChangesAsync(CancellationToken);
        }
    }

    /// <summary>
    /// Сохраняет персонажа и его мувы
    /// </summary>
    protected async Task SaveCharacterAndMoves(TekkenCharacter character, Move[] moves)
    {
        if (Options.UseStagingService && StagingService != null)
        {
            // Через сервис ожидающих изменений
            await StagingService.StageCharacterAndMoves(
                character,
                moves,
                options?.IsSupplementMode ?? false
            );
        }
        else
        {
            // Напрямую в базу данных
            await using var db = await DbContextFactory.CreateDbContextAsync(CancellationToken);

            // Сохраняем персонажа
            var existingChar = await db.TekkenCharacters.FirstOrDefaultAsync(
                c => c.Name == character.Name,
                CancellationToken
            );

            if (existingChar == null)
            {
                await db.TekkenCharacters.AddAsync(character, CancellationToken);
            }
            else
            {
                if (Options.IsSupplementMode)
                {
                    // В режиме дополнения заполняем только пустые поля
                    var supplementedChar = SupplementCharacter(existingChar, character);
                    db.Entry(existingChar).CurrentValues.SetValues(supplementedChar);
                }
                else
                {
                    db.Entry(existingChar).CurrentValues.SetValues(character);
                }
            }

            // Сохраняем мувы
            // Use the tracked character instance to avoid tracking duplicates
            var trackedCharacter = existingChar ?? character;

            foreach (var move in moves)
            {
                // Ensure move refers to the tracked character to avoid EF attaching a different instance
                move.CharacterName = trackedCharacter.Name;
                move.Character = trackedCharacter;

                var existingMove = await db.TekkenMoves.FirstOrDefaultAsync(
                    m => m.CharacterName == move.CharacterName && m.Command == move.Command,
                    CancellationToken
                );

                if (existingMove == null)
                {
                    await db.TekkenMoves.AddAsync(move, CancellationToken);
                }
                else
                {
                    if (Options.IsSupplementMode)
                    {
                        // В режиме дополнения заполняем только пустые поля
                        var supplementedMove = SupplementMove(existingMove, move);
                        db.Entry(existingMove).CurrentValues.SetValues(supplementedMove);
                    }
                    else
                    {
                        db.Entry(existingMove).CurrentValues.SetValues(move);
                    }
                }
            }

            await db.SaveChangesAsync(CancellationToken);
        }
    }

    /// <summary>
    /// Объединяет данные персонажа в режиме дополнения
    /// </summary>
    private static TekkenCharacter SupplementCharacter(
        TekkenCharacter existing,
        TekkenCharacter supplement
    )
    {
        return new TekkenCharacter
        {
            Name = existing.Name,
            LinkToImage = existing.LinkToImage ?? supplement.LinkToImage,
            PageUrl = existing.PageUrl ?? supplement.PageUrl,
            Image = existing.Image ?? supplement.Image,
            ImageExtension = existing.ImageExtension ?? supplement.ImageExtension,
            AvatarImage = existing.AvatarImage ?? supplement.AvatarImage,
            AvatarImageExtension = existing.AvatarImageExtension ?? supplement.AvatarImageExtension,
            FullBodyImage = existing.FullBodyImage ?? supplement.FullBodyImage,
            FullBodyImageExtension =
                existing.FullBodyImageExtension ?? supplement.FullBodyImageExtension,
            LastUpdateTime = existing.LastUpdateTime,
            Description = existing.Description ?? supplement.Description,
            Strengths = existing.Strengths ?? supplement.Strengths,
            Weaknesess = existing.Weaknesess ?? supplement.Weaknesess,
        };
    }

    /// <summary>
    /// Объединяет данные мува в режиме дополнения
    /// </summary>
    private static Move SupplementMove(Move existing, Move supplement)
    {
        return new Move
        {
            CharacterName = existing.CharacterName,
            Command = existing.Command,
            StanceCode = existing.StanceCode ?? supplement.StanceCode,
            StanceName = existing.StanceName ?? supplement.StanceName,
            HeatEngage = existing.HeatEngage || supplement.HeatEngage,
            HeatSmash = existing.HeatSmash || supplement.HeatSmash,
            PowerCrush = existing.PowerCrush || supplement.PowerCrush,
            Throw = existing.Throw || supplement.Throw,
            Homing = existing.Homing || supplement.Homing,
            Tornado = existing.Tornado || supplement.Tornado,
            HeatBurst = existing.HeatBurst || supplement.HeatBurst,
            RequiresHeat = existing.RequiresHeat || supplement.RequiresHeat,
            HitLevel = existing.HitLevel ?? supplement.HitLevel,
            Damage = existing.Damage ?? supplement.Damage,
            StartUpFrame = existing.StartUpFrame ?? supplement.StartUpFrame,
            BlockFrame = existing.BlockFrame ?? supplement.BlockFrame,
            HitFrame = existing.HitFrame ?? supplement.HitFrame,
            CounterHitFrame = existing.CounterHitFrame ?? supplement.CounterHitFrame,
            Notes = existing.Notes, // Временно исключаем Notes из логики дополнения
        };
    }

    /// <summary>
    /// Задержка между запросами
    /// </summary>
    protected async Task DelayBetweenRequests()
    {
        if (Options.RequestDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(Options.RequestDelaySeconds), CancellationToken);
        }
    }

    /// <summary>
    /// Задержка между персонажами
    /// </summary>
    protected async Task DelayBetweenCharacters()
    {
        if (Options.CharacterDelaySeconds > 0)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(Options.CharacterDelaySeconds),
                CancellationToken
            );
        }
    }

    /// <summary>
    /// Консолидирует группы мувов
    /// </summary>
    protected static Task<Move[]> ConsolidateMoveGroups(List<Move> moves)
    {
        var groupedMoves = moves.GroupBy(m => new { m.CharacterName, m.Command });

        var consolidatedMoves = new List<Move>();
        var uniqueMoves = new List<Move>();

        foreach (var group in groupedMoves)
        {
            if (group.Count() > 1)
            {
                // Консолидируем дублирующиеся мувы
                var consolidatedMove = new Move
                {
                    CharacterName = group.Key.CharacterName,
                    Command = group.Key.Command,
                    HeatEngage = group.Any(m => m.HeatEngage),
                    HeatSmash = group.Any(m => m.HeatSmash),
                    PowerCrush = group.Any(m => m.PowerCrush),
                    Throw = group.Any(m => m.Throw),
                    Homing = group.Any(m => m.Homing),
                    Tornado = group.Any(m => m.Tornado),
                    HeatBurst = group.Any(m => m.HeatBurst),
                    RequiresHeat = group.Any(m => m.RequiresHeat),
                    StanceCode = group.First().StanceCode,
                    HitLevel = group.First().HitLevel,
                    Damage = group.First().Damage,
                    StartUpFrame = group.First().StartUpFrame,
                    BlockFrame = group.First().BlockFrame,
                    HitFrame = group.First().HitFrame,
                    CounterHitFrame = group.First().CounterHitFrame,
                    Notes = [.. group.SelectMany(m => m.Notes ?? [])],
                };

                consolidatedMoves.Add(consolidatedMove);
            }
            else
            {
                uniqueMoves.Add(group.First());
            }
        }

        var result = new Move[uniqueMoves.Count + consolidatedMoves.Count];
        uniqueMoves.CopyTo(result, 0);
        consolidatedMoves.CopyTo(result, uniqueMoves.Count);

        return Task.FromResult(result);
    }
}
