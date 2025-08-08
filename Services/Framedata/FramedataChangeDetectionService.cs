using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.XPath;
using MARS.Server.Services.Framedata.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Framedata;

/// <summary>
/// Сервис для обнаружения и отслеживания изменений в фреймдате Tekken 8
/// </summary>
public class FramedataChangeDetectionService(
    ILogger<FramedataChangeDetectionService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime lifetime
)
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;
    private static readonly Uri BasePath = new("https://wavu.wiki/w/");

    /// <summary>
    /// Запускает процесс обнаружения изменений в фреймдате
    /// </summary>
    public async Task StartScrupFrameData(Chat? chat = default)
    {
        try
        {
            logger.LogInformation("Начинаю обнаружение изменений в фреймдате Tekken 8");

            var config = AngleSharp.Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            var doc = await context.OpenAsync(
                BasePath.AbsoluteUri + "t/Main_Page",
                _cancellationToken
            );

            // Найти контейнер выбора персонажа
            var charSelectContainer = doc.QuerySelector("div.char-select-t8");

            if (charSelectContainer == null)
            {
                logger.LogWarning("Не удалось найти контейнер выбора персонажей");
                return;
            }

            // Все div с персонажами
            var charDivs = charSelectContainer.QuerySelectorAll("div.char-select-t8-img");
            var detectedChanges = new List<FramedataChange>();

            foreach (var charDiv in charDivs)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _cancellationToken);

                // Получить ссылку на страницу персонажа
                var aNode = charDiv.QuerySelector("a");
                var href = aNode?.GetAttribute("href");

                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var charPagePath = BasePath.AbsoluteUri + href;
                var characterName = ExtractCharacterNameFromUrl(href);

                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                logger.LogInformation("Проверяю персонажа: {CharacterName}", characterName);

                // Проверяем изменения для персонажа
                var characterChanges = await DetectCharacterChanges(
                    characterName,
                    charPagePath,
                    context
                );
                detectedChanges.AddRange(characterChanges);
            }

            // Сохраняем обнаруженные изменения
            if (detectedChanges.Count > 0)
            {
                await SaveDetectedChanges(detectedChanges);
                logger.LogInformation(
                    "Обнаружено {Count} изменений в фреймдате",
                    detectedChanges.Count
                );

                if (chat != null)
                {
                    // Здесь можно добавить уведомление в Telegram
                    // await SendTelegramNotification(chat, detectedChanges);
                }
            }
            else
            {
                logger.LogInformation("Изменений в фреймдате не обнаружено");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обнаружении изменений в фреймдате");
        }
    }

    /// <summary>
    /// Обнаруживает изменения для конкретного персонажа
    /// </summary>
    private async Task<List<FramedataChange>> DetectCharacterChanges(
        string characterName,
        string characterPageUrl,
        IBrowsingContext context
    )
    {
        var changes = new List<FramedataChange>();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        // Проверяем существование персонажа в БД
        var existingCharacter = await dbContext
            .TekkenCharacters.Include(c => c.Movelist)
            .FirstOrDefaultAsync(c => c.Name == characterName, _cancellationToken);

        var charPage = await context.OpenAsync(characterPageUrl, _cancellationToken);

        // Получаем текущую информацию о персонаже с сайта
        var currentCharacterInfo = await ExtractCharacterInfo(charPage, characterName);
        var currentMoves = await ExtractMovesInfo(charPage, characterName);

        if (existingCharacter == null)
        {
            // Новый персонаж
            var change = new FramedataChange
            {
                CharacterName = characterName,
                ChangeType = FramedataChangeType.NewCharacter,
                Description = $"Обнаружен новый персонаж: {characterName}",
                Status = FramedataChangeStatus.Pending,
            };

            // Создаем информацию об изменении
            var changeInfo = new FramedataChangeInfo
            {
                InfoType = FramedataInfoType.Character,
                JsonData = JsonSerializer.Serialize(currentCharacterInfo),
                SourceUrl = characterPageUrl,
                DataHash = CalculateHash(JsonSerializer.Serialize(currentCharacterInfo)),
            };

            change.ChangeInfo = changeInfo;
            changeInfo.FramedataChange = change;
            changes.Add(change);
        }
        else
        {
            // Проверяем изменения в информации о персонаже
            var characterChanges = await DetectCharacterInfoChanges(
                existingCharacter,
                currentCharacterInfo,
                characterPageUrl
            );
            changes.AddRange(characterChanges);

            // Проверяем изменения в ходах
            var moveChanges = await DetectMoveChanges(
                existingCharacter,
                currentMoves,
                characterPageUrl
            );
            changes.AddRange(moveChanges);
        }

        return changes;
    }

    /// <summary>
    /// Обнаруживает изменения в информации о персонаже
    /// </summary>
    private static Task<List<FramedataChange>> DetectCharacterInfoChanges(
        TekkenCharacter existingCharacter,
        TekkenCharacter currentCharacterInfo,
        string sourceUrl
    )
    {
        var changes = new List<FramedataChange>();

        var existingHash = CalculateHash(JsonSerializer.Serialize(existingCharacter));
        var currentHash = CalculateHash(JsonSerializer.Serialize(currentCharacterInfo));

        if (existingHash != currentHash)
        {
            var change = new FramedataChange
            {
                CharacterName = existingCharacter.Name,
                ChangeType = FramedataChangeType.CharacterUpdate,
                Description = $"Обновлена информация о персонаже: {existingCharacter.Name}",
                Status = FramedataChangeStatus.Pending,
            };

            // Информация о текущем состоянии
            var currentInfo = new FramedataChangeInfo
            {
                InfoType = FramedataInfoType.Character,
                JsonData = JsonSerializer.Serialize(existingCharacter),
                SourceUrl = existingCharacter.PageUrl,
                DataHash = existingHash,
            };

            // Информация о новом состоянии
            var changeInfo = new FramedataChangeInfo
            {
                InfoType = FramedataInfoType.Character,
                JsonData = JsonSerializer.Serialize(currentCharacterInfo),
                SourceUrl = sourceUrl,
                DataHash = currentHash,
            };

            change.CurrentInfo = currentInfo;
            change.ChangeInfo = changeInfo;
            changeInfo.FramedataChange = change;
            changes.Add(change);
        }

        return Task.FromResult(changes);
    }

    /// <summary>
    /// Обнаруживает изменения в ходах персонажа
    /// </summary>
    private static Task<List<FramedataChange>> DetectMoveChanges(
        TekkenCharacter existingCharacter,
        List<Move> currentMoves,
        string sourceUrl
    )
    {
        var changes = new List<FramedataChange>();

        var existingMoves = existingCharacter.Movelist?.ToList() ?? [];
        var existingMovesDict = existingMoves.ToDictionary(m => m.Command, m => m);
        var currentMovesDict = currentMoves.ToDictionary(m => m.Command, m => m);

        // Проверяем новые ходы
        foreach (var currentMove in currentMoves)
        {
            if (!existingMovesDict.ContainsKey(currentMove.Command))
            {
                var change = new FramedataChange
                {
                    CharacterName = existingCharacter.Name,
                    ChangeType = FramedataChangeType.NewMove,
                    Description = $"Новый ход для {existingCharacter.Name}: {currentMove.Command}",
                    Status = FramedataChangeStatus.Pending,
                };

                var changeInfo = new FramedataChangeInfo
                {
                    InfoType = FramedataInfoType.Move,
                    JsonData = JsonSerializer.Serialize(currentMove),
                    SourceUrl = sourceUrl,
                    DataHash = CalculateHash(JsonSerializer.Serialize(currentMove)),
                };

                change.ChangeInfo = changeInfo;
                changeInfo.FramedataChange = change;
                changes.Add(change);
            }
        }

        // Проверяем удаленные ходы
        foreach (var existingMove in existingMoves)
        {
            if (!currentMovesDict.ContainsKey(existingMove.Command))
            {
                var change = new FramedataChange
                {
                    CharacterName = existingCharacter.Name,
                    ChangeType = FramedataChangeType.MoveRemoval,
                    Description =
                        $"Удален ход для {existingCharacter.Name}: {existingMove.Command}",
                    Status = FramedataChangeStatus.Pending,
                };

                var currentInfo = new FramedataChangeInfo
                {
                    InfoType = FramedataInfoType.Move,
                    JsonData = JsonSerializer.Serialize(existingMove),
                    SourceUrl = existingCharacter.PageUrl,
                    DataHash = CalculateHash(JsonSerializer.Serialize(existingMove)),
                };

                change.CurrentInfo = currentInfo;
                changes.Add(change);
            }
        }

        // Проверяем изменения в существующих ходах
        foreach (var currentMove in currentMoves)
        {
            if (existingMovesDict.TryGetValue(currentMove.Command, out var existingMove))
            {
                var existingHash = CalculateHash(JsonSerializer.Serialize(existingMove));
                var currentHash = CalculateHash(JsonSerializer.Serialize(currentMove));

                if (existingHash != currentHash)
                {
                    var change = new FramedataChange
                    {
                        CharacterName = existingCharacter.Name,
                        ChangeType = FramedataChangeType.MoveUpdate,
                        Description =
                            $"Обновлен ход для {existingCharacter.Name}: {currentMove.Command}",
                        Status = FramedataChangeStatus.Pending,
                    };

                    var currentInfo = new FramedataChangeInfo
                    {
                        InfoType = FramedataInfoType.Move,
                        JsonData = JsonSerializer.Serialize(existingMove),
                        SourceUrl = existingCharacter.PageUrl,
                        DataHash = existingHash,
                    };

                    var changeInfo = new FramedataChangeInfo
                    {
                        InfoType = FramedataInfoType.Move,
                        JsonData = JsonSerializer.Serialize(currentMove),
                        SourceUrl = sourceUrl,
                        DataHash = currentHash,
                    };

                    change.CurrentInfo = currentInfo;
                    change.ChangeInfo = changeInfo;
                    changeInfo.FramedataChange = change;
                    changes.Add(change);
                }
            }
        }

        return Task.FromResult(changes);
    }

    /// <summary>
    /// Извлекает информацию о персонаже со страницы
    /// </summary>
    private static Task<TekkenCharacter> ExtractCharacterInfo(
        IDocument charPage,
        string characterName
    )
    {
        var divOutput =
            charPage.Body.SelectSingleNode("//*[@id=\"mw-content-text\"]/div[1]")
            ?? throw new Exception("Не удалось найти контент страницы");

        var divOutputElement =
            divOutput as IElement ?? throw new Exception("divOutput не является IElement");

        // Извлекаем описание
        var pS = divOutputElement.QuerySelectorAll("p");
        var stringBuilder = new StringBuilder();
        foreach (var node in pS)
        {
            if (!node.TextContent.Contains("This page is"))
            {
                stringBuilder.Append(node.TextContent);
            }
        }
        var description = stringBuilder.ToString();

        // Извлекаем сильные и слабые стороны
        var federa =
            divOutputElement.SelectSingleNode(".//div[contains(@style, 'display: grid;')]")
            ?? throw new Exception("Не удалось найти информацию о сильных/слабых сторонах");

        var federaElement =
            federa as IElement ?? throw new Exception("federa не является IElement");

        var strAndWkns = federaElement.QuerySelectorAll("ul");
        var listStr = new List<string>();
        var listWknss = new List<string>();

        for (var index = 0; index < strAndWkns.Length; index++)
        {
            var za = strAndWkns[index];
            var twfs = za.QuerySelectorAll("li");
            foreach (var htmlNode in twfs)
            {
                var innerGrps = htmlNode.TextContent;
                switch (index)
                {
                    case 0:
                        listStr.Add(innerGrps);
                        break;
                    case 1:
                        listWknss.Add(innerGrps);
                        break;
                }
            }
        }

        return Task.FromResult(
            new TekkenCharacter
            {
                Name = characterName,
                Description = description,
                Strengths = [.. listStr],
                Weaknesess = [.. listWknss],
                PageUrl = charPage.Url,
                LastUpdateTime = DateTimeOffset.Now,
            }
        );
    }

    /// <summary>
    /// Извлекает информацию о ходах со страницы
    /// </summary>
    private static Task<List<Move>> ExtractMovesInfo(IDocument charPage, string characterName)
    {
        var movelist = new List<Move>();

        // Находим таблицу с мувлистом
        var tableNode = charPage.QuerySelector("table.cargoTable > tbody");

        // Проверяем, что таблица найдена
        var rowNodes = tableNode?.QuerySelectorAll("tr");

        // Проверяем, что строки таблицы найдены
        if (rowNodes == null)
        {
            return Task.FromResult(movelist);
        }

        foreach (var rowNode in rowNodes)
        {
            // Получаем ячейки (столбцы) текущей строки
            var cellNodes = rowNode.QuerySelectorAll("td[class]");

            if (cellNodes is not { Length: >= 9 })
            {
                continue;
            }

            var command = cellNodes[0].TextContent.Trim();

            // Создаем новый объект Move
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            var move = new Move
            {
                CharacterName = characterName,
                Command = command.Split('-').Last().Trim().ToLower(),
            };

            move.Command = move.Command.Replace(".", " ");

            // Заполняем остальные свойства объекта Move данными из остальных ячеек
            move.StartUpFrame = cellNodes[1].TextContent.Trim().ToLower();
            move.HitLevel = cellNodes[2].TextContent.Trim().ToLower();
            move.Damage = cellNodes[3].TextContent.Trim().ToLower();
            move.BlockFrame = cellNodes[4].TextContent.Trim().ToLower();
            move.HitFrame = cellNodes[5].TextContent.Trim().ToLower();
            move.CounterHitFrame = cellNodes[6].TextContent.Trim().ToLower();
            move.Notes = cellNodes[8].TextContent.Trim().ToLower();

            // Parse states if needed (cellNodes[7])

            var notes = move.Notes.ToLower();
            if (!string.IsNullOrWhiteSpace(notes))
            {
                if (notes.Contains("power crush"))
                {
                    move.PowerCrush = true;
                }

                if (notes.Contains("heat burst"))
                {
                    move.HeatBurst = true;
                }

                if (notes.Contains("heat engager"))
                {
                    move.HeatEngage = true;
                }

                if (notes.Contains("heat smash"))
                {
                    move.HeatSmash = true;
                }

                if (move.Command.StartsWith('h'))
                {
                    move.RequiresHeat = true;
                }

                if (notes.Contains("tornado"))
                {
                    move.Tornado = true;
                }

                if (notes.Contains("homing"))
                {
                    move.Homing = true;
                }

                if (notes.Contains("throw"))
                {
                    move.Throw = true;
                }
            }

            movelist.Add(move);
        }

        return Task.FromResult(movelist);
    }

    /// <summary>
    /// Извлекает имя персонажа из URL
    /// </summary>
    private static string? ExtractCharacterNameFromUrl(string href)
    {
        // Пример URL: /w/t/Character_Name
        var parts = href.Split('/');
        return parts.Length >= 3 ? parts[^1].Replace('_', ' ') : null;
    }

    /// <summary>
    /// Вычисляет хеш строки
    /// </summary>
    private static string CalculateHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Сохраняет обнаруженные изменения в БД
    /// </summary>
    private async Task SaveDetectedChanges(List<FramedataChange> changes)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        foreach (var change in changes)
        {
            // Проверяем, нет ли уже такого изменения
            var existingChange = await dbContext
                .Set<FramedataChange>()
                .FirstOrDefaultAsync(
                    c =>
                        c.CharacterName == change.CharacterName
                        && c.ChangeType == change.ChangeType
                        && c.Status == FramedataChangeStatus.Pending,
                    _cancellationToken
                );

            if (existingChange == null)
            {
                // Добавляем изменение и связанные сущности
                dbContext.Set<FramedataChange>().Add(change);

                if (change.ChangeInfo != null)
                {
                    dbContext.Set<FramedataChangeInfo>().Add(change.ChangeInfo);
                }

                if (change.CurrentInfo != null)
                {
                    dbContext.Set<FramedataChangeInfo>().Add(change.CurrentInfo);
                }
            }
        }

        await dbContext.SaveChangesAsync(_cancellationToken);
    }

    /// <summary>
    /// Применяет изменение к базе данных
    /// </summary>
    public async Task ApplyChange(int changeId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        var change =
            await dbContext
                .Set<FramedataChange>()
                .Include(c => c.ChangeInfo)
                .Include(c => c.CurrentInfo)
                .FirstOrDefaultAsync(c => c.Id == changeId, _cancellationToken)
            ?? throw new ArgumentException($"Изменение с ID {changeId} не найдено");
        if (change.Status != FramedataChangeStatus.Pending)
        {
            throw new InvalidOperationException($"Изменение уже имеет статус {change.Status}");
        }

        try
        {
            switch (change.ChangeType)
            {
                case FramedataChangeType.NewCharacter:
                    await ApplyNewCharacter(change, dbContext);
                    break;
                case FramedataChangeType.CharacterUpdate:
                    await ApplyCharacterUpdate(change, dbContext);
                    break;
                case FramedataChangeType.NewMove:
                    await ApplyNewMove(change, dbContext);
                    break;
                case FramedataChangeType.MoveUpdate:
                    await ApplyMoveUpdate(change, dbContext);
                    break;
                case FramedataChangeType.MoveRemoval:
                    await ApplyMoveRemoval(change, dbContext);
                    break;
            }

            change.Status = FramedataChangeStatus.Applied;
            change.AppliedAt = DateTimeOffset.Now;
            await dbContext.SaveChangesAsync(_cancellationToken);

            logger.LogInformation("Изменение {ChangeId} успешно применено", changeId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при применении изменения {ChangeId}", changeId);
            throw;
        }
    }

    /// <summary>
    /// Применяет нового персонажа
    /// </summary>
    private static Task ApplyNewCharacter(FramedataChange change, AppDbContext dbContext)
    {
        if (change.ChangeInfo?.JsonData == null)
        {
            throw new InvalidOperationException("Отсутствует информация об изменении");
        }

        var character =
            JsonSerializer.Deserialize<TekkenCharacter>(change.ChangeInfo.JsonData)
            ?? throw new InvalidOperationException(
                "Не удалось десериализовать информацию о персонаже"
            );
        dbContext.TekkenCharacters.Add(character);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Применяет обновление персонажа
    /// </summary>
    private async Task ApplyCharacterUpdate(FramedataChange change, AppDbContext dbContext)
    {
        if (change.ChangeInfo?.JsonData == null)
        {
            throw new InvalidOperationException("Отсутствует информация об изменении");
        }

        var updatedCharacter =
            JsonSerializer.Deserialize<TekkenCharacter>(change.ChangeInfo.JsonData)
            ?? throw new InvalidOperationException(
                "Не удалось десериализовать информацию о персонаже"
            );
        var existingCharacter =
            await dbContext.TekkenCharacters.FirstOrDefaultAsync(
                c => c.Name == change.CharacterName,
                _cancellationToken
            ) ?? throw new InvalidOperationException($"Персонаж {change.CharacterName} не найден");

        // Обновляем свойства персонажа
        existingCharacter.Description = updatedCharacter.Description;
        existingCharacter.Strengths = updatedCharacter.Strengths;
        existingCharacter.Weaknesess = updatedCharacter.Weaknesess;
        existingCharacter.PageUrl = updatedCharacter.PageUrl;
        existingCharacter.LastUpdateTime = DateTimeOffset.Now;
    }

    /// <summary>
    /// Применяет новый ход
    /// </summary>
    private static Task ApplyNewMove(FramedataChange change, AppDbContext dbContext)
    {
        if (change.ChangeInfo?.JsonData == null)
        {
            throw new InvalidOperationException("Отсутствует информация об изменении");
        }

        var move =
            JsonSerializer.Deserialize<Move>(change.ChangeInfo.JsonData)
            ?? throw new InvalidOperationException("Не удалось десериализовать информацию о ходе");
        dbContext.TekkenMoves.Add(move);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Применяет обновление хода
    /// </summary>
    private async Task ApplyMoveUpdate(FramedataChange change, AppDbContext dbContext)
    {
        if (change.ChangeInfo?.JsonData == null)
        {
            throw new InvalidOperationException("Отсутствует информация об изменении");
        }

        var updatedMove =
            JsonSerializer.Deserialize<Move>(change.ChangeInfo.JsonData)
            ?? throw new InvalidOperationException("Не удалось десериализовать информацию о ходе");
        var existingMove =
            await dbContext.TekkenMoves.FirstOrDefaultAsync(
                m => m.CharacterName == change.CharacterName && m.Command == updatedMove.Command,
                _cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Ход {updatedMove.Command} для {change.CharacterName} не найден"
            );

        // Обновляем свойства хода
        existingMove.StanceCode = updatedMove.StanceCode;
        existingMove.StanceName = updatedMove.StanceName;
        existingMove.HeatEngage = updatedMove.HeatEngage;
        existingMove.HeatSmash = updatedMove.HeatSmash;
        existingMove.PowerCrush = updatedMove.PowerCrush;
        existingMove.Throw = updatedMove.Throw;
        existingMove.Homing = updatedMove.Homing;
        existingMove.Tornado = updatedMove.Tornado;
        existingMove.HeatBurst = updatedMove.HeatBurst;
        existingMove.RequiresHeat = updatedMove.RequiresHeat;
        existingMove.HitLevel = updatedMove.HitLevel;
        existingMove.Damage = updatedMove.Damage;
        existingMove.StartUpFrame = updatedMove.StartUpFrame;
        existingMove.BlockFrame = updatedMove.BlockFrame;
        existingMove.HitFrame = updatedMove.HitFrame;
        existingMove.CounterHitFrame = updatedMove.CounterHitFrame;
        existingMove.Notes = updatedMove.Notes;
    }

    /// <summary>
    /// Применяет удаление хода
    /// </summary>
    private async Task ApplyMoveRemoval(FramedataChange change, AppDbContext dbContext)
    {
        if (change.CurrentInfo?.JsonData == null)
        {
            throw new InvalidOperationException("Отсутствует информация о текущем состоянии");
        }

        var move =
            JsonSerializer.Deserialize<Move>(change.CurrentInfo.JsonData)
            ?? throw new InvalidOperationException("Не удалось десериализовать информацию о ходе");
        var existingMove = await dbContext.TekkenMoves.FirstOrDefaultAsync(
            m => m.CharacterName == change.CharacterName && m.Command == move.Command,
            _cancellationToken
        );

        if (existingMove != null)
        {
            dbContext.TekkenMoves.Remove(existingMove);
        }
    }

    /// <summary>
    /// Получает список ожидающих изменений
    /// </summary>
    public async Task<List<FramedataChange>> GetPendingChanges()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        return await dbContext
            .Set<FramedataChange>()
            .Include(c => c.ChangeInfo)
            .Include(c => c.CurrentInfo)
            .Where(c => c.Status == FramedataChangeStatus.Pending)
            .OrderBy(c => c.DetectedAt)
            .ToListAsync(_cancellationToken);
    }

    /// <summary>
    /// Отклоняет изменение
    /// </summary>
    public async Task RejectChange(int changeId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_cancellationToken);

        var change =
            await dbContext
                .Set<FramedataChange>()
                .FirstOrDefaultAsync(c => c.Id == changeId, _cancellationToken)
            ?? throw new ArgumentException($"Изменение с ID {changeId} не найдено");
        change.Status = FramedataChangeStatus.Rejected;
        await dbContext.SaveChangesAsync(_cancellationToken);

        logger.LogInformation("Изменение {ChangeId} отклонено", changeId);
    }

    /// <summary>
    /// Обнаруживает изменения для персонажа с готовыми данными
    /// </summary>
    public async Task DetectChangesForCharacter(TekkenCharacter character, Move[] moves)
    {
        try
        {
            logger.LogInformation(
                "Проверяю изменения для персонажа: {CharacterName}",
                character.Name
            );

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                _cancellationToken
            );
            var detectedChanges = new List<FramedataChange>();

            // Проверяем изменения информации о персонаже
            var existingCharacter = await dbContext.TekkenCharacters.FirstOrDefaultAsync(
                c => c.Name == character.Name,
                _cancellationToken
            );

            if (existingCharacter == null)
            {
                // Новый персонаж
                var change = new FramedataChange
                {
                    CharacterName = character.Name,
                    ChangeType = FramedataChangeType.NewCharacter,
                    DetectedAt = DateTime.UtcNow,
                    Status = FramedataChangeStatus.Pending,
                    Description = $"Новый персонаж: {character.Name}",
                    ChangeInfo = new FramedataChangeInfo
                    {
                        InfoType = FramedataInfoType.Character,
                        JsonData = JsonSerializer.Serialize(character),
                        RetrievedAt = DateTimeOffset.UtcNow,
                    },
                };
                detectedChanges.Add(change);
            }
            else
            {
                // Проверяем изменения в информации о персонаже
                var characterHash = CalculateHash(JsonSerializer.Serialize(character));
                var existingCharacterHash = CalculateHash(
                    JsonSerializer.Serialize(existingCharacter)
                );

                if (characterHash != existingCharacterHash)
                {
                    var change = new FramedataChange
                    {
                        CharacterName = character.Name,
                        ChangeType = FramedataChangeType.CharacterUpdate,
                        DetectedAt = DateTime.UtcNow,
                        Status = FramedataChangeStatus.Pending,
                        Description = $"Обновление информации о персонаже: {character.Name}",
                        ChangeInfo = new FramedataChangeInfo
                        {
                            InfoType = FramedataInfoType.Character,
                            JsonData = JsonSerializer.Serialize(character),
                            RetrievedAt = DateTimeOffset.UtcNow,
                        },
                        CurrentInfo = new FramedataChangeInfo
                        {
                            InfoType = FramedataInfoType.Character,
                            JsonData = JsonSerializer.Serialize(existingCharacter),
                            RetrievedAt = DateTimeOffset.UtcNow,
                        },
                    };
                    detectedChanges.Add(change);
                }
            }

            // Проверяем изменения в ходах
            var existingMoves = await dbContext
                .TekkenMoves.Where(m => m.CharacterName == character.Name)
                .ToListAsync(_cancellationToken);

            var currentMovesDict = moves.ToDictionary(m => m.Command, m => m);
            var existingMovesDict = existingMoves.ToDictionary(m => m.Command, m => m);

            // Находим новые ходы
            foreach (var move in moves)
            {
                if (!existingMovesDict.ContainsKey(move.Command))
                {
                    var change = new FramedataChange
                    {
                        CharacterName = character.Name,
                        ChangeType = FramedataChangeType.NewMove,
                        DetectedAt = DateTime.UtcNow,
                        Status = FramedataChangeStatus.Pending,
                        Description = $"Новый ход для {character.Name}: {move.Command}",
                        ChangeInfo = new FramedataChangeInfo
                        {
                            InfoType = FramedataInfoType.Move,
                            JsonData = JsonSerializer.Serialize(move),
                            RetrievedAt = DateTimeOffset.UtcNow,
                        },
                    };
                    detectedChanges.Add(change);
                }
            }

            // Находим обновленные ходы
            foreach (var move in moves)
            {
                if (existingMovesDict.TryGetValue(move.Command, out var existingMove))
                {
                    var moveHash = CalculateHash(JsonSerializer.Serialize(move));
                    var existingMoveHash = CalculateHash(JsonSerializer.Serialize(existingMove));

                    if (moveHash != existingMoveHash)
                    {
                        var change = new FramedataChange
                        {
                            CharacterName = character.Name,
                            ChangeType = FramedataChangeType.MoveUpdate,
                            DetectedAt = DateTime.UtcNow,
                            Status = FramedataChangeStatus.Pending,
                            Description = $"Обновление хода для {character.Name}: {move.Command}",
                            ChangeInfo = new FramedataChangeInfo
                            {
                                InfoType = FramedataInfoType.Move,
                                JsonData = JsonSerializer.Serialize(move),
                                RetrievedAt = DateTimeOffset.UtcNow,
                            },
                            CurrentInfo = new FramedataChangeInfo
                            {
                                InfoType = FramedataInfoType.Move,
                                JsonData = JsonSerializer.Serialize(existingMove),
                                RetrievedAt = DateTimeOffset.UtcNow,
                            },
                        };
                        detectedChanges.Add(change);
                    }
                }
            }

            // Находим удаленные ходы
            foreach (var existingMove in existingMoves)
            {
                if (!currentMovesDict.ContainsKey(existingMove.Command))
                {
                    var change = new FramedataChange
                    {
                        CharacterName = character.Name,
                        ChangeType = FramedataChangeType.MoveRemoval,
                        DetectedAt = DateTime.UtcNow,
                        Status = FramedataChangeStatus.Pending,
                        Description = $"Удаление хода для {character.Name}: {existingMove.Command}",
                        CurrentInfo = new FramedataChangeInfo
                        {
                            InfoType = FramedataInfoType.Move,
                            JsonData = JsonSerializer.Serialize(existingMove),
                            RetrievedAt = DateTimeOffset.UtcNow,
                        },
                    };
                    detectedChanges.Add(change);
                }
            }

            // Сохраняем обнаруженные изменения
            if (detectedChanges.Count > 0)
            {
                await SaveDetectedChanges(detectedChanges);
                logger.LogInformation(
                    "Обнаружено {Count} изменений для персонажа {CharacterName}",
                    detectedChanges.Count,
                    character.Name
                );
            }
            else
            {
                logger.LogInformation(
                    "Изменений для персонажа {CharacterName} не обнаружено",
                    character.Name
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обнаружении изменений для персонажа {CharacterName}",
                character.Name
            );
        }
    }
}
