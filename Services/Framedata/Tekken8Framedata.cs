using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Entitys.Enums;

namespace MARS.Server.Services.Framedata;

public partial class Tekken8FrameData(
    ILogger<Tekken8FrameData> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime lifetime,
    ITelegramBotClient client
) : BackgroundService, ITelegramusService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);
        var list = dbContext.TekkenCharacters.AsNoTracking().ToList();
        var character = list.FirstOrDefault();
        if (
            character == null
            || !IsDateInCurrentWeek(character.LastUpdateTime).GetAwaiter().GetResult()
        )
        {
            await Task.Factory.StartNew(() => StartScrupFrameData(), stoppingToken);
        }

        await UpdateMovesForVictorina();
    }

    public async Task<Move[]?> GetCharMoveList(string charname)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );
        var character = await dbContext
            .TekkenCharacters.Include(e => e.Movelist)
            .AsNoTracking()
            .FirstAsync(e => e.Name.Equals(charname), cancellationToken: _cancellationToken);

        return character.Movelist?.ToArray();
    }

    public async Task<Move?> GetMoveAsync(string[] command)
    {
        TekkenCharacter? charnameOut = null;

        var length = 2;

        var charname = string.Join(" ", command.Take(length));

        var isCharFounded = false;
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );

        //
        foreach (var aliasPair in Aliases.CharacterNameAliases)
        {
            if (aliasPair.Key.Equals(charname) || aliasPair.Value.Any(e => e.Equals(charname)))
            {
                var character = aliasPair.Key;

                var characters = dbContext.TekkenCharacters.AsAsyncEnumerable();
                await foreach (TekkenCharacter tekkenCharacter in characters)
                {
                    if (tekkenCharacter.Name.Equals(character, StringComparison.OrdinalIgnoreCase))
                    {
                        charnameOut = tekkenCharacter;
                        isCharFounded = true;
                        break;
                    }
                }
            }
        }

        if (!isCharFounded)
        {
            --length;
            charname = string.Join(" ", command.Take(length));

            foreach (KeyValuePair<string, string[]> pair in Aliases.CharacterNameAliases)
            {
                if (pair.Key.Equals(charname) || pair.Value.Any(e => e.Equals(charname)))
                {
                    var character = pair.Key;

                    var characters = dbContext.TekkenCharacters.AsAsyncEnumerable();
                    await foreach (TekkenCharacter pairCharacter in characters)
                    {
                        if (pairCharacter.Name.Equals(character))
                        {
                            charnameOut = pairCharacter;
                            break;
                        }
                    }
                }
            }
        }

        if (command.Length - length == 0 || charnameOut is null)
        {
            return null;
        }

        var input = string.Join(" ", command.Skip(length)).ToLower();

        if (string.IsNullOrWhiteSpace(charnameOut.Name) || string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var movelist = dbContext
            .TekkenMoves.AsNoTracking()
            .Where(e => e.Character == charnameOut)
            .Include(e => e.Character)
            .ToList();

        if (movelist is { Count: > 0 })
        {
            var move =
                await GetMoveFromMovelistByCommandAsync(input, movelist)
                ?? await GetMoveFromMovelistByTagAsync(input, movelist);

            return move;
        }

        return null;
    }

    private static Task<Move?> GetMoveFromMovelistByTagAsync(string input, List<Move> movelist)
    {
        Move? move = null;

        var typeWithoutStance = MoveTags
            .FirstOrDefault(e =>
                e.Value.Any(b => b.Equals(input, StringComparison.InvariantCulture))
            )
            .Key;

        if (typeWithoutStance == TekkenMoveTag.None)
        {
            move = movelist.FirstOrDefault(e =>
                (e.StanceName?.Equals(input) ?? false) || e.StanceCode.Equals(input)
            );

            return Task.FromResult(move);
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

        return Task.FromResult(move);
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

        var allMoves = dbContext
            .TekkenMoves.Include(e => e.Character)
            .AsNoTracking()
            .AsAsyncEnumerable();
        var list = new List<Move>();
        await foreach (var move in allMoves)
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

        VictorinaMoves = list;
    }
}
