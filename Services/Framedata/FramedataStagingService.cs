using System.Text.Json;
using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Entitys.Pending;

namespace MARS.Server.Services.Framedata;

// DTO модели для фронтенда с вычисляемым полем IsNew
public class TekkenCharacterPendingDto : TekkenCharacterPending
{
    public bool IsNew { get; set; }
}

public class MovePendingDto : MovePending
{
    public bool IsNew { get; set; }
}

public class FramedataStagingService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime lifetime
)
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    public async Task StageCharacterAndMoves(
        TekkenCharacter character,
        Move[] moves,
        bool isSupplementMode = false
    )
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        // Character: stage if new or changed
        var existingCharacter = await db
            .TekkenCharacters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == character.Name, _cancellationToken);
        var pendingCharacter = await db.TekkenCharactersPending.FirstOrDefaultAsync(
            c => c.Name == character.Name,
            _cancellationToken
        );

        if (
            existingCharacter == null
            || HasSignificantChanges(existingCharacter, character, isSupplementMode)
        )
        {
            var staged = MapToPending(character);

            if (pendingCharacter == null)
            {
                await db.TekkenCharactersPending.AddAsync(staged, _cancellationToken);
            }
            else
            {
                db.Entry(pendingCharacter).CurrentValues.SetValues(staged);
            }
        }

        // Moves: stage only new or changed
        var existingMoves = await db
            .TekkenMoves.AsNoTracking()
            .Where(m => m.CharacterName == character.Name)
            .ToDictionaryAsync(m => m.Command, _cancellationToken);
        var pendingMoves = await db
            .TekkenMovesPending.Where(m => m.CharacterName == character.Name)
            .ToDictionaryAsync(m => m.Command, _cancellationToken);

        foreach (var move in moves)
        {
            if (
                !existingMoves.TryGetValue(move.Command, out var existing)
                || HasSignificantChanges(existing, move, isSupplementMode)
            )
            {
                var stagedMove = MapToPending(move);

                if (!pendingMoves.TryGetValue(move.Command, out var pending))
                {
                    await db.TekkenMovesPending.AddAsync(stagedMove, _cancellationToken);
                }
                else
                {
                    db.Entry(pending).CurrentValues.SetValues(stagedMove);
                }
            }
        }

        await db.SaveChangesAsync(_cancellationToken);
    }

    public async Task<List<TekkenCharacterPendingDto>> GetPendingCharacters()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);
        var pendingCharacters = await db
            .TekkenCharactersPending.AsNoTracking()
            .ToListAsync(_cancellationToken);

        var result = new List<TekkenCharacterPendingDto>();
        foreach (var pc in pendingCharacters)
        {
            var existing = await db
                .TekkenCharacters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == pc.Name, _cancellationToken);

            var dto = new TekkenCharacterPendingDto
            {
                Name = pc.Name,
                LinkToImage = pc.LinkToImage,
                PageUrl = pc.PageUrl,
                Image = pc.Image,
                ImageExtension = pc.ImageExtension,
                AvatarImage = pc.AvatarImage,
                AvatarImageExtension = pc.AvatarImageExtension,
                FullBodyImage = pc.FullBodyImage,
                FullBodyImageExtension = pc.FullBodyImageExtension,
                LastUpdateTime = pc.LastUpdateTime,
                Description = pc.Description,
                Strengths = pc.Strengths,
                Weaknesess = pc.Weaknesess,
                IsNew = existing == null, // true если персонаж новый, false если обновление
            };

            result.Add(dto);
        }

        return result;
    }

    public async Task<List<MovePendingDto>> GetPendingMoves(string? characterName = null)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);
        var query = db.TekkenMovesPending.AsQueryable();
        if (!string.IsNullOrWhiteSpace(characterName))
        {
            query = query.Where(m => m.CharacterName == characterName);
        }

        var pendingMoves = await query.AsNoTracking().ToListAsync(_cancellationToken);

        var result = new List<MovePendingDto>();
        foreach (var pm in pendingMoves)
        {
            var existing = await db
                .TekkenMoves.AsNoTracking()
                .FirstOrDefaultAsync(
                    m => m.CharacterName == pm.CharacterName && m.Command == pm.Command,
                    _cancellationToken
                );

            var dto = new MovePendingDto
            {
                CharacterName = pm.CharacterName,
                Command = pm.Command,
                StanceCode = pm.StanceCode,
                StanceName = pm.StanceName,
                HeatEngage = pm.HeatEngage,
                HeatSmash = pm.HeatSmash,
                PowerCrush = pm.PowerCrush,
                Throw = pm.Throw,
                Homing = pm.Homing,
                Tornado = pm.Tornado,
                HeatBurst = pm.HeatBurst,
                RequiresHeat = pm.RequiresHeat,
                HitLevel = pm.HitLevel,
                Damage = pm.Damage,
                StartUpFrame = pm.StartUpFrame,
                BlockFrame = pm.BlockFrame,
                HitFrame = pm.HitFrame,
                CounterHitFrame = pm.CounterHitFrame,
                Notes = pm.Notes,
                IsNew = existing == null, // true если ход новый, false если обновление
            };

            result.Add(dto);
        }

        return result;
    }

    public async Task ApproveCharacter(string name)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);
        var pending =
            await db.TekkenCharactersPending.FirstOrDefaultAsync(
                c => c.Name == name,
                _cancellationToken
            ) ?? throw new ArgumentException($"Pending character {name} not found");
        var existing = await db.TekkenCharacters.FirstOrDefaultAsync(
            c => c.Name == name,
            _cancellationToken
        );

        if (existing == null)
        {
            var mapped = MapFromPending(pending);
            await db.TekkenCharacters.AddAsync(mapped, _cancellationToken);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(MapFromPending(pending));
        }

        // Сначала удаляем все связанные pending moves, чтобы избежать нарушения ограничения внешнего ключа
        var relatedMoves = await db
            .TekkenMovesPending.Where(m => m.CharacterName == name)
            .ToListAsync(_cancellationToken);

        if (relatedMoves.Count != 0)
        {
            db.TekkenMovesPending.RemoveRange(relatedMoves);
        }

        db.TekkenCharactersPending.Remove(pending);
        await db.SaveChangesAsync(_cancellationToken);
    }

    public async Task RejectCharacter(string name)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);
        var pending =
            await db.TekkenCharactersPending.FirstOrDefaultAsync(
                c => c.Name == name,
                _cancellationToken
            ) ?? throw new ArgumentException($"Pending character {name} not found");

        // Сначала удаляем все связанные pending moves, чтобы избежать нарушения ограничения внешнего ключа
        var relatedMoves = await db
            .TekkenMovesPending.Where(m => m.CharacterName == name)
            .ToListAsync(_cancellationToken);

        if (relatedMoves.Count != 0)
        {
            db.TekkenMovesPending.RemoveRange(relatedMoves);
        }

        db.TekkenCharactersPending.Remove(pending);
        await db.SaveChangesAsync(_cancellationToken);
    }

    public async Task ApproveMove(string characterName, string command)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);
        var pending =
            await db.TekkenMovesPending.FirstOrDefaultAsync(
                m => m.CharacterName == characterName && m.Command == command,
                _cancellationToken
            ) ?? throw new ArgumentException($"Pending move {characterName}:{command} not found");
        var existing = await db.TekkenMoves.FirstOrDefaultAsync(
            m => m.CharacterName == characterName && m.Command == command,
            _cancellationToken
        );

        if (existing == null)
        {
            var mapped = MapFromPending(pending);
            await db.TekkenMoves.AddAsync(mapped, _cancellationToken);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(MapFromPending(pending));
        }

        db.TekkenMovesPending.Remove(pending);
        await db.SaveChangesAsync(_cancellationToken);
    }

    public async Task RejectMove(string characterName, string command)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);
        var pending =
            await db.TekkenMovesPending.FirstOrDefaultAsync(
                m => m.CharacterName == characterName && m.Command == command,
                _cancellationToken
            ) ?? throw new ArgumentException($"Pending move {characterName}:{command} not found");
        db.TekkenMovesPending.Remove(pending);
        await db.SaveChangesAsync(_cancellationToken);
    }

    public async Task ApproveAll()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        const int batchSize = 100;

        // Обрабатываем персонажей батчами
        var totalCharactersProcessed = 0;
        while (true)
        {
            var pendingCharsBatch = await db
                .TekkenCharactersPending.OrderBy(c => c.Name)
                .Skip(totalCharactersProcessed)
                .Take(batchSize)
                .ToListAsync(_cancellationToken);

            if (pendingCharsBatch.Count == 0)
            {
                break;
            }

            foreach (var pc in pendingCharsBatch)
            {
                var existing = await db.TekkenCharacters.FirstOrDefaultAsync(
                    c => c.Name == pc.Name,
                    _cancellationToken
                );
                if (existing == null)
                {
                    await db.TekkenCharacters.AddAsync(MapFromPending(pc), _cancellationToken);
                }
                else
                {
                    db.Entry(existing).CurrentValues.SetValues(MapFromPending(pc));
                }
            }

            await db.SaveChangesAsync(_cancellationToken);
            totalCharactersProcessed += pendingCharsBatch.Count;

            if (pendingCharsBatch.Count < batchSize)
            {
                break;
            }
        }

        // Обрабатываем мувы батчами
        var totalMovesProcessed = 0;
        while (true)
        {
            var pendingMovesBatch = await db
                .TekkenMovesPending.OrderBy(m => m.CharacterName)
                .ThenBy(m => m.Command)
                .Skip(totalMovesProcessed)
                .Take(batchSize)
                .ToListAsync(_cancellationToken);

            if (pendingMovesBatch.Count == 0)
            {
                break;
            }

            foreach (var pm in pendingMovesBatch)
            {
                var existing = await db.TekkenMoves.FirstOrDefaultAsync(
                    m => m.CharacterName == pm.CharacterName && m.Command == pm.Command,
                    _cancellationToken
                );
                if (existing == null)
                {
                    await db.TekkenMoves.AddAsync(MapFromPending(pm), _cancellationToken);
                }
                else
                {
                    db.Entry(existing).CurrentValues.SetValues(MapFromPending(pm));
                }
            }

            await db.SaveChangesAsync(_cancellationToken);
            totalMovesProcessed += pendingMovesBatch.Count;

            if (pendingMovesBatch.Count < batchSize)
            {
                break;
            }
        }

        // Удаляем все pending записи батчами
        // Сначала удаляем pending moves, затем pending characters
        // чтобы избежать нарушения ограничения внешнего ключа
        await db.TekkenMovesPending.ExecuteDeleteAsync(cancellationToken: _cancellationToken);
        await db.TekkenCharactersPending.ExecuteDeleteAsync(cancellationToken: _cancellationToken);
    }

    public async Task RejectAll()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(_cancellationToken);
        // Сначала удаляем pending moves, затем pending characters
        // чтобы избежать нарушения ограничения внешнего ключа
        await db.TekkenMovesPending.ExecuteDeleteAsync(cancellationToken: _cancellationToken);
        await db.TekkenCharactersPending.ExecuteDeleteAsync(cancellationToken: _cancellationToken);
        await db.SaveChangesAsync(_cancellationToken);
    }

    private static bool EqualsByJson<T>(T left, T right)
    {
        var l = JsonSerializer.Serialize(left);
        var r = JsonSerializer.Serialize(right);
        return string.Equals(l, r, StringComparison.Ordinal);
    }

    private static TekkenCharacterPending MapToPending(TekkenCharacter character) =>
        new()
        {
            Name = character.Name,
            LinkToImage = character.LinkToImage,
            PageUrl = character.PageUrl,
            Image = character.Image,
            ImageExtension = character.ImageExtension,
            AvatarImage = character.AvatarImage,
            AvatarImageExtension = character.AvatarImageExtension,
            FullBodyImage = character.FullBodyImage,
            FullBodyImageExtension = character.FullBodyImageExtension,
            LastUpdateTime = character.LastUpdateTime,
            Description = character.Description,
            Strengths = character.Strengths,
            Weaknesess = character.Weaknesess,
        };

    private static TekkenCharacter MapFromPending(TekkenCharacterPending c) =>
        new()
        {
            Name = c.Name,
            LinkToImage = c.LinkToImage,
            PageUrl = c.PageUrl,
            Image = c.Image,
            ImageExtension = c.ImageExtension,
            AvatarImage = c.AvatarImage,
            AvatarImageExtension = c.AvatarImageExtension,
            FullBodyImage = c.FullBodyImage,
            FullBodyImageExtension = c.FullBodyImageExtension,
            LastUpdateTime = c.LastUpdateTime,
            Description = c.Description,
            Strengths = c.Strengths,
            Weaknesess = c.Weaknesess,
        };

    private static MovePending MapToPending(Move move) =>
        new()
        {
            CharacterName = move.CharacterName,
            Command = move.Command,
            StanceCode = move.StanceCode,
            StanceName = move.StanceName,
            HeatEngage = move.HeatEngage,
            HeatSmash = move.HeatSmash,
            PowerCrush = move.PowerCrush,
            Throw = move.Throw,
            Homing = move.Homing,
            Tornado = move.Tornado,
            HeatBurst = move.HeatBurst,
            RequiresHeat = move.RequiresHeat,
            HitLevel = move.HitLevel,
            Damage = move.Damage,
            StartUpFrame = move.StartUpFrame,
            BlockFrame = move.BlockFrame,
            HitFrame = move.HitFrame,
            CounterHitFrame = move.CounterHitFrame,
            Notes = move.Notes?.ToArray(),
        };

    private static Move MapFromPending(MovePending move) =>
        new()
        {
            CharacterName = move.CharacterName,
            Command = move.Command,
            StanceCode = move.StanceCode,
            StanceName = move.StanceName,
            HeatEngage = move.HeatEngage,
            HeatSmash = move.HeatSmash,
            PowerCrush = move.PowerCrush,
            Throw = move.Throw,
            Homing = move.Homing,
            Tornado = move.Tornado,
            HeatBurst = move.HeatBurst,
            RequiresHeat = move.RequiresHeat,
            HitLevel = move.HitLevel,
            Damage = move.Damage,
            StartUpFrame = move.StartUpFrame,
            BlockFrame = move.BlockFrame,
            HitFrame = move.HitFrame,
            CounterHitFrame = move.CounterHitFrame,
            Notes = move.Notes?.ToArray(),
        };

    /// <summary>
    /// Проверяет, есть ли значимые изменения между объектами
    /// </summary>
    private static bool HasSignificantChanges<T>(T left, T right, bool isSupplementMode)
        where T : class
    {
        var leftJson = JsonSerializer.Serialize(left);
        var rightJson = JsonSerializer.Serialize(right);

        if (string.Equals(leftJson, rightJson, StringComparison.Ordinal))
        {
            return false;
        }

        // Дополнительная проверка для null значений
        var leftObj = JsonSerializer.Deserialize<JsonElement>(leftJson);
        var rightObj = JsonSerializer.Deserialize<JsonElement>(rightJson);

        return HasNonNullChanges(leftObj, rightObj, isSupplementMode);
    }

    /// <summary>
    /// Рекурсивно проверяет, есть ли изменения в не-null значениях
    /// </summary>
    private static bool HasNonNullChanges(
        JsonElement left,
        JsonElement right,
        bool isSupplementMode
    )
    {
        if (left.ValueKind != right.ValueKind)
        {
            return true;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in left.EnumerateObject())
                {
                    if (right.TryGetProperty(property.Name, out var rightValue))
                    {
                        // Если левое значение null, а правое не null - это значимое изменение
                        if (
                            property.Value.ValueKind == JsonValueKind.Null
                            && rightValue.ValueKind != JsonValueKind.Null
                        )
                        {
                            return true;
                        }

                        // Если оба значения не null, проверяем рекурсивно
                        if (
                            property.Value.ValueKind != JsonValueKind.Null
                            && rightValue.ValueKind != JsonValueKind.Null
                            && isSupplementMode
                        )
                        {
                            // Специальная обработка для поля Notes
                            if (property.Name == "Notes")
                            {
                                if (!HasNotesSignificantChanges(property.Value, rightValue))
                                {
                                    continue; // Изменения в Notes незначимы, продолжаем проверку других полей
                                }
                            }

                            if (HasNonNullChanges(property.Value, rightValue, isSupplementMode))
                            {
                                return true;
                            }
                        }
                    }
                }
                break;

            case JsonValueKind.Array:
                if (left.GetArrayLength() != right.GetArrayLength())
                {
                    return true;
                }

                for (var i = 0; i < left.GetArrayLength(); i++)
                {
                    if (HasNonNullChanges(left[i], right[i], isSupplementMode))
                    {
                        return true;
                    }
                }
                break;

            default:
                if (left.ValueKind != JsonValueKind.Null && right.ValueKind != JsonValueKind.Null)
                {
                    return !left.GetRawText().Equals(right.GetRawText());
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, есть ли значимые изменения в Notes
    /// </summary>
    private static bool HasNotesSignificantChanges(JsonElement leftNotes, JsonElement rightNotes)
    {
        // Если оба массива имеют одинаковую длину - изменения незначимы
        if (
            leftNotes.ValueKind == JsonValueKind.Array
            && rightNotes.ValueKind == JsonValueKind.Array
        )
        {
            return leftNotes.GetArrayLength() != rightNotes.GetArrayLength();
        }

        // Если один из них не массив - это значимое изменение
        return leftNotes.ValueKind != JsonValueKind.Array
            || rightNotes.ValueKind != JsonValueKind.Array;
    }
}
