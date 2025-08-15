using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Entitys.Pending;
using MARS.Server.Services.Framedata.Subservices.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
            await StagingService.StageCharacterAndMoves(character, []);
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
            await StagingService.StageCharacterAndMoves(character, moves);
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
                db.Entry(existingChar).CurrentValues.SetValues(character);
            }

            // Сохраняем мувы
            foreach (var move in moves)
            {
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
                    db.Entry(existingMove).CurrentValues.SetValues(move);
                }
            }

            await db.SaveChangesAsync(CancellationToken);
        }
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
                    Notes = group.SelectMany(m => m.Notes ?? []).ToArray(),
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
