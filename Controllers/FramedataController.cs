using MARS.Server.Services;
using MARS.Server.Services.Framedata;
using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Subservices.Entitys;
using MARS.Server.Services.Framedata.Subservices.HtmlParsers;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для работы с фреймдатой Tekken
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FramedataController(
    IDbContextFactory<AppDbContext> factory,
    ILogger<FramedataController> logger
) : ControllerBase
{
    /// <summary>
    /// Получить персонажей с пагинацией (без мувлистов)
    /// </summary>
    /// <param name="page">Номер страницы (начиная с 1)</param>
    /// <param name="pageSize">Размер страницы (по умолчанию 20, максимум 100)</param>
    /// <returns>Список персонажей</returns>
    [HttpGet("characters")]
    public async Task<ActionResult<OperationResult<PagedResult<TekkenCharacter>>>> GetCharacters(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20
    )
    {
        ActionResult<OperationResult<PagedResult<TekkenCharacter>>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 20;
            }

            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var skip = (page - 1) * pageSize;

            var totalCount = await dbContext.TekkenCharacters.CountAsync();

            var characters = await dbContext
                .TekkenCharacters.AsNoTracking()
                .OrderBy(c => c.Name)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            var pagedResult = new PagedResult<TekkenCharacter>
            {
                Items = characters,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            };

            result = Ok(
                OperationResult<PagedResult<TekkenCharacter>>.Ok(
                    "Получены персонажи",
                    pagedResult
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении персонажей");
            result = Ok(
                OperationResult<PagedResult<TekkenCharacter>>.Bad(
                    "Ошибка при получении персонажей",
                    new PagedResult<TekkenCharacter>()
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить персонажа по имени (без мувлиста)
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Персонаж</returns>
    [HttpGet("characters/{name}")]
    public async Task<ActionResult<OperationResult<TekkenCharacter?>>> GetCharacter(string name)
    {
        ActionResult<OperationResult<TekkenCharacter?>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var character = await dbContext
                .TekkenCharacters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == name);

            if (character != null)
            {
                result = Ok(OperationResult<TekkenCharacter?>.Ok("Персонаж найден", character));
            }
            else
            {
                result = Ok(
                    OperationResult<TekkenCharacter?>.Bad($"Персонаж '{name}' не найден", null)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении персонажа {Name}", name);
            result = Ok(
                OperationResult<TekkenCharacter?>.Bad("Ошибка при получении персонажа", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Создать нового персонажа
    /// </summary>
    /// <param name="character">Данные персонажа</param>
    /// <returns>Созданный персонаж</returns>
    [HttpPost("characters")]
    public async Task<ActionResult<OperationResult<TekkenCharacter?>>> CreateCharacter(
        [FromBody] TekkenCharacter character
    )
    {
        ActionResult<OperationResult<TekkenCharacter?>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            if (await dbContext.TekkenCharacters.AnyAsync(c => c.Name == character.Name))
            {
                result = Ok(
                    OperationResult<TekkenCharacter?>.Bad(
                        $"Персонаж с именем '{character.Name}' уже существует",
                        null
                    )
                );
            }
            else
            {
                character.LastUpdateTime = DateTimeOffset.Now;
                dbContext.TekkenCharacters.Add(character);
                await dbContext.SaveChangesAsync();

                result = Ok(
                    OperationResult<TekkenCharacter?>.Ok("Персонаж успешно создан", character)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании персонажа {Name}", character.Name);
            result = Ok(
                OperationResult<TekkenCharacter?>.Bad("Ошибка при создании персонажа", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Обновить персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <param name="character">Обновленные данные</param>
    /// <returns>Обновленный персонаж</returns>
    [HttpPut("characters/{name}")]
    public async Task<ActionResult<OperationResult<TekkenCharacter?>>> UpdateCharacter(
        string name,
        [FromBody] TekkenCharacter character
    )
    {
        ActionResult<OperationResult<TekkenCharacter?>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var existingCharacter = await dbContext
                .TekkenCharacters.Include(c => c.Movelist)
                .FirstOrDefaultAsync(c => c.Name == name);

            if (existingCharacter == null)
            {
                result = Ok(
                    OperationResult<TekkenCharacter?>.Bad($"Персонаж '{name}' не найден", null)
                );
            }
            else
            {
                // Обновляем свойства персонажа
                existingCharacter.LinkToImage = character.LinkToImage;
                existingCharacter.PageUrl = character.PageUrl;
                existingCharacter.Image = character.Image;
                existingCharacter.ImageExtension = character.ImageExtension;
                existingCharacter.Description = character.Description;
                existingCharacter.Strengths = character.Strengths;
                existingCharacter.Weaknesess = character.Weaknesess;
                existingCharacter.LastUpdateTime = DateTimeOffset.Now;

                await dbContext.SaveChangesAsync();

                result = Ok(
                    OperationResult<TekkenCharacter?>.Ok(
                        "Персонаж успешно обновлен",
                        existingCharacter
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении персонажа {Name}", name);
            result = Ok(
                OperationResult<TekkenCharacter?>.Bad("Ошибка при обновлении персонажа", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Удалить персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Результат удаления</returns>
    [HttpDelete("characters/{name}")]
    public async Task<ActionResult<OperationResult>> DeleteCharacter(string name)
    {
        ActionResult<OperationResult> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var character = await dbContext
                .TekkenCharacters.Include(c => c.Movelist)
                .FirstOrDefaultAsync(c => c.Name == name);

            if (character == null)
            {
                result = Ok(OperationResult.Bad($"Персонаж '{name}' не найден"));
            }
            else
            {
                dbContext.TekkenCharacters.Remove(character);
                await dbContext.SaveChangesAsync();

                result = Ok(OperationResult.Ok("Персонаж успешно удален"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении персонажа {Name}", name);
            result = Ok(OperationResult.Bad("Ошибка при удалении персонажа"));
        }

        return result;
    }

    /// <summary>
    /// Получить движения персонажа с пагинацией
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="page">Номер страницы (начиная с 1)</param>
    /// <param name="pageSize">Размер страницы (по умолчанию 50, максимум 200)</param>
    /// <returns>Список движений</returns>
    [HttpGet("characters/{characterName}/moves")]
    public async Task<ActionResult<OperationResult<PagedResult<Move>>>> GetCharacterMoves(
        string characterName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50
    )
    {
        ActionResult<OperationResult<PagedResult<Move>>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            if (pageSize > 200)
            {
                pageSize = 200;
            }

            var skip = (page - 1) * pageSize;

            var totalCount = await dbContext
                .TekkenMoves.AsNoTracking()
                .Where(m => m.CharacterName == characterName)
                .CountAsync();

            var moves = await dbContext
                .TekkenMoves.AsNoTracking()
                .Where(m => m.CharacterName == characterName)
                .OrderBy(m => m.Command)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            var pagedResult = new PagedResult<Move>
            {
                Items = moves,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            };

            result = Ok(
                OperationResult<PagedResult<Move>>.Ok("Получены движения персонажа", pagedResult)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении движений персонажа {CharacterName}",
                characterName
            );
            result = Ok(
                OperationResult<PagedResult<Move>>.Bad(
                    "Ошибка при получении движений персонажа",
                    new PagedResult<Move>()
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить конкретное движение
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="command">Команда движения</param>
    /// <returns>Движение</returns>
    [HttpGet("characters/{characterName}/moves/{command}")]
    public async Task<ActionResult<OperationResult<Move?>>> GetMove(
        string characterName,
        string command
    )
    {
        ActionResult<OperationResult<Move?>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var move = await dbContext.TekkenMoves.FirstOrDefaultAsync(m =>
                m.CharacterName == characterName && m.Command == command
            );

            if (move != null)
            {
                result = Ok(OperationResult<Move?>.Ok("Движение найдено", move));
            }
            else
            {
                result = Ok(
                    OperationResult<Move?>.Bad(
                        $"Движение '{command}' для персонажа '{characterName}' не найдено",
                        null
                    )
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении движения {Command} для персонажа {CharacterName}",
                command,
                characterName
            );
            result = Ok(OperationResult<Move?>.Bad("Ошибка при получении движения", null));
        }

        return result;
    }

    /// <summary>
    /// Создать новое движение
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="move">Данные движения</param>
    /// <returns>Созданное движение</returns>
    [HttpPost("characters/{characterName}/moves")]
    public async Task<ActionResult<OperationResult<Move?>>> CreateMove(
        string characterName,
        [FromBody] Move move
    )
    {
        ActionResult<OperationResult<Move?>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            // Проверяем существование персонажа
            var character = await dbContext.TekkenCharacters.FirstOrDefaultAsync(c =>
                c.Name == characterName
            );
            if (character == null)
            {
                result = Ok(
                    OperationResult<Move?>.Bad($"Персонаж '{characterName}' не найден", null)
                );
            }
            else if (
                await dbContext.TekkenMoves.AnyAsync(m =>
                    m.CharacterName == characterName && m.Command == move.Command
                )
            )
            {
                result = Ok(
                    OperationResult<Move?>.Bad(
                        $"Движение '{move.Command}' для персонажа '{characterName}' уже существует",
                        null
                    )
                );
            }
            else
            {
                move.CharacterName = characterName;
                dbContext.TekkenMoves.Add(move);
                await dbContext.SaveChangesAsync();

                result = Ok(OperationResult<Move?>.Ok("Движение успешно создано", move));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при создании движения {Command} для персонажа {CharacterName}",
                move.Command,
                characterName
            );
            result = Ok(OperationResult<Move?>.Bad("Ошибка при создании движения", null));
        }

        return result;
    }

    /// <summary>
    /// Обновить движение
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="command">Команда движения</param>
    /// <param name="move">Обновленные данные движения</param>
    /// <returns>Обновленное движение</returns>
    [HttpPut("characters/{characterName}/moves/{command}")]
    public async Task<ActionResult<OperationResult<Move?>>> UpdateMove(
        string characterName,
        string command,
        [FromBody] Move move
    )
    {
        ActionResult<OperationResult<Move?>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var existingMove = await dbContext.TekkenMoves.FirstOrDefaultAsync(m =>
                m.CharacterName == characterName && m.Command == command
            );

            if (existingMove == null)
            {
                result = Ok(
                    OperationResult<Move?>.Bad(
                        $"Движение '{command}' для персонажа '{characterName}' не найдено",
                        null
                    )
                );
            }
            else
            {
                // Обновляем свойства движения
                existingMove.StanceCode = move.StanceCode;
                existingMove.StanceName = move.StanceName;
                existingMove.HeatEngage = move.HeatEngage;
                existingMove.HeatSmash = move.HeatSmash;
                existingMove.PowerCrush = move.PowerCrush;
                existingMove.Throw = move.Throw;
                existingMove.Homing = move.Homing;
                existingMove.Tornado = move.Tornado;
                existingMove.HeatBurst = move.HeatBurst;
                existingMove.RequiresHeat = move.RequiresHeat;
                existingMove.HitLevel = move.HitLevel;
                existingMove.Damage = move.Damage;
                existingMove.StartUpFrame = move.StartUpFrame;
                existingMove.BlockFrame = move.BlockFrame;
                existingMove.HitFrame = move.HitFrame;
                existingMove.CounterHitFrame = move.CounterHitFrame;
                existingMove.Notes = move.Notes?.ToArray();

                await dbContext.SaveChangesAsync();

                result = Ok(OperationResult<Move?>.Ok("Движение успешно обновлено", existingMove));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обновлении движения {Command} для персонажа {CharacterName}",
                command,
                characterName
            );
            result = Ok(OperationResult<Move?>.Bad("Ошибка при обновлении движения", null));
        }

        return result;
    }

    /// <summary>
    /// Удалить движение
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="command">Команда движения</param>
    /// <returns>Результат удаления</returns>
    [HttpDelete("characters/{characterName}/moves/{command}")]
    public async Task<ActionResult<OperationResult>> DeleteMove(
        string characterName,
        string command
    )
    {
        ActionResult<OperationResult> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var move = await dbContext.TekkenMoves.FirstOrDefaultAsync(m =>
                m.CharacterName == characterName && m.Command == command
            );

            if (move == null)
            {
                result = Ok(
                    OperationResult.Bad(
                        $"Движение '{command}' для персонажа '{characterName}' не найдено"
                    )
                );
            }
            else
            {
                dbContext.TekkenMoves.Remove(move);
                await dbContext.SaveChangesAsync();

                result = Ok(OperationResult.Ok("Движение успешно удалено"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при удалении движения {Command} для персонажа {CharacterName}",
                command,
                characterName
            );
            result = Ok(OperationResult.Bad("Ошибка при удалении движения"));
        }

        return result;
    }

    /// <summary>
    /// Поиск движений по фильтрам с пагинацией
    /// </summary>
    /// <param name="characterName">Имя персонажа (опционально)</param>
    /// <param name="stanceCode">Код стойки (опционально)</param>
    /// <param name="heatEngage">Требует ли Heat Engage (опционально)</param>
    /// <param name="powerCrush">Является ли Power Crush (опционально)</param>
    /// <param name="isThrow">Является ли броском (опционально)</param>
    /// <param name="homing">Является ли Homing (опционально)</param>
    /// <param name="page">Номер страницы (начиная с 1)</param>
    /// <param name="pageSize">Размер страницы (по умолчанию 50, максимум 200)</param>
    /// <returns>Отфильтрованные движения</returns>
    [HttpGet("moves/search")]
    public async Task<ActionResult<OperationResult<PagedResult<Move>>>> SearchMoves(
        [FromQuery] string? characterName = null,
        [FromQuery] string? stanceCode = null,
        [FromQuery] bool? heatEngage = null,
        [FromQuery] bool? powerCrush = null,
        [FromQuery] bool? isThrow = null,
        [FromQuery] bool? homing = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50
    )
    {
        ActionResult<OperationResult<PagedResult<Move>>> result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var query = dbContext.TekkenMoves.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(characterName))
            {
                query = query.Where(m => m.CharacterName == characterName);
            }

            if (!string.IsNullOrEmpty(stanceCode))
            {
                query = query.Where(m => m.StanceCode == stanceCode);
            }

            if (heatEngage.HasValue)
            {
                query = query.Where(m => m.HeatEngage == heatEngage.Value);
            }

            if (powerCrush.HasValue)
            {
                query = query.Where(m => m.PowerCrush == powerCrush.Value);
            }

            if (isThrow.HasValue)
            {
                query = query.Where(m => m.Throw == isThrow.Value);
            }

            if (homing.HasValue)
            {
                query = query.Where(m => m.Homing == homing.Value);
            }

            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            if (pageSize > 200)
            {
                pageSize = 200;
            }

            var skip = (page - 1) * pageSize;

            var totalCount = await query.CountAsync();

            var moves = await query
                .OrderBy(m => m.CharacterName)
                .ThenBy(m => m.Command)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            var pagedResult = new PagedResult<Move>
            {
                Items = moves,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            };

            result = Ok(OperationResult<PagedResult<Move>>.Ok("Движения найдены", pagedResult));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при поиске движений");
            result = Ok(
                OperationResult<PagedResult<Move>>.Bad(
                    "Ошибка при поиске движений",
                    new PagedResult<Move>()
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Получить изображение персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Изображение персонажа</returns>
    [HttpGet("characters/{name}/image")]
    public async Task<ActionResult> GetCharacterImage(string name)
    {
        ActionResult result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var character = await dbContext.TekkenCharacters.FirstOrDefaultAsync(c =>
                c.Name == name
            );

            if (character == null)
            {
                result = NotFound($"Персонаж '{name}' не найден");
            }
            else if (character.Image == null || character.Image.Length == 0)
            {
                result = NotFound($"Изображение для персонажа '{name}' не найдено");
            }
            else
            {
                var contentType = GetContentType(character.ImageExtension);
                result = File(character.Image, contentType);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении изображения персонажа {Name}", name);
            result = StatusCode(500, "Внутренняя ошибка сервера");
        }

        return result;
    }

    /// <summary>
    /// Получить аватар персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Аватар персонажа</returns>
    [HttpGet("characters/{name}/avatar")]
    public async Task<ActionResult> GetCharacterAvatar(string name)
    {
        ActionResult result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var character = await dbContext.TekkenCharacters.FirstOrDefaultAsync(c =>
                c.Name == name
            );

            if (character == null)
            {
                result = NotFound($"Персонаж '{name}' не найден");
            }
            else if (character.AvatarImage == null || character.AvatarImage.Length == 0)
            {
                result = NotFound($"Аватар для персонажа '{name}' не найден");
            }
            else
            {
                var contentType = GetContentType(character.AvatarImageExtension);
                result = File(character.AvatarImage, contentType);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении аватара персонажа {Name}", name);
            result = StatusCode(500, "Внутренняя ошибка сервера");
        }

        return result;
    }

    /// <summary>
    /// Получить полное изображение персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Полное изображение персонажа</returns>
    [HttpGet("characters/{name}/fullbody")]
    public async Task<ActionResult> GetCharacterFullBody(string name)
    {
        ActionResult result = null!;

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var character = await dbContext.TekkenCharacters.FirstOrDefaultAsync(c =>
                c.Name == name
            );

            if (character == null)
            {
                result = NotFound($"Персонаж '{name}' не найден");
            }
            else if (character.FullBodyImage == null || character.FullBodyImage.Length == 0)
            {
                result = NotFound($"Полное изображение для персонажа '{name}' не найдено");
            }
            else
            {
                var contentType = GetContentType(character.FullBodyImageExtension);
                result = File(character.FullBodyImage, contentType);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении полного изображения персонажа {Name}", name);
            result = StatusCode(500, "Внутренняя ошибка сервера");
        }

        return result;
    }

    private static string GetContentType(string? extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }

    /// <summary>
    /// Запустить парсинг фреймдаты с указанными настройками
    /// </summary>
    /// <param name="request">Запрос на парсинг</param>
    /// <returns>Результат парсинга</returns>
    [HttpPost("parse")]
    public async Task<ActionResult<OperationResult<ParseResult>>> ParseFramedata(
        [FromBody] ParseRequest request
    )
    {
        ActionResult<OperationResult<ParseResult>> result;
        try
        {
            var options = new FramedataParserOptions
            {
                RequestDelaySeconds = request.RequestDelaySeconds ?? 2,
                CharacterDelaySeconds = request.CharacterDelaySeconds ?? 5,
                UseStagingService = request.UseStagingService ?? true,
                ParseMoves = request.ParseMoves ?? true,
                MaxRetries = request.MaxRetries ?? 3,
                HttpTimeoutSeconds = request.HttpTimeoutSeconds ?? 30,
            };

            var parser = FramedataParserFactory.CreateParser(
                request.Source,
                logger,
                factory,
                null, // StagingService будет добавлен позже
                CancellationToken.None,
                options
            );

            var parsedCharacters = await parser.ParseCharactersAndMoves(request.CharacterNames);

            var parseResult = new ParseResult
            {
                Success = true,
                ParsedCharacters = parsedCharacters,
                Message = $"Успешно распарсено {parsedCharacters.Count} персонажей",
            };

            result = Ok(OperationResult<ParseResult>.Ok("Парсинг выполнен успешно", parseResult));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при парсинге фреймдаты");
            var errorResult = new ParseResult
            {
                Success = false,
                Message = $"Ошибка при парсинге: {ex.Message}",
            };
            result = Ok(
                OperationResult<ParseResult>.Bad("Ошибка при парсинге фреймдаты", errorResult)
            );
        }

        return result;
    }

    /// <summary>
    /// Запустить парсинг только персонажей (без мувов)
    /// </summary>
    /// <param name="request">Запрос на парсинг</param>
    /// <returns>Результат парсинга</returns>
    [HttpPost("parse-characters-only")]
    public async Task<ActionResult<OperationResult<ParseResult>>> ParseCharactersOnly(
        [FromBody] ParseRequest request
    )
    {
        ActionResult<OperationResult<ParseResult>> result;
        try
        {
            var options = new FramedataParserOptions
            {
                RequestDelaySeconds = request.RequestDelaySeconds ?? 2,
                CharacterDelaySeconds = request.CharacterDelaySeconds ?? 5,
                UseStagingService = request.UseStagingService ?? true,
                ParseMoves = false, // Всегда false для этого метода
                MaxRetries = request.MaxRetries ?? 3,
                HttpTimeoutSeconds = request.HttpTimeoutSeconds ?? 30,
            };

            var parser = FramedataParserFactory.CreateParser(
                request.Source,
                logger,
                factory,
                null, // StagingService будет добавлен позже
                CancellationToken.None,
                options
            );

            var parsedCharacters = await parser.ParseCharactersOnly(request.CharacterNames);

            var parseResult = new ParseResult
            {
                Success = true,
                ParsedCharacters = parsedCharacters,
                Message = $"Успешно распарсено {parsedCharacters.Count} персонажей (без мувов)",
            };

            result = Ok(
                OperationResult<ParseResult>.Ok("Парсинг персонажей выполнен успешно", parseResult)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при парсинге персонажей");
            var errorResult = new ParseResult
            {
                Success = false,
                Message = $"Ошибка при парсинге: {ex.Message}",
            };
            result = Ok(
                OperationResult<ParseResult>.Bad("Ошибка при парсинге персонажей", errorResult)
            );
        }

        return result;
    }

    /// <summary>
    /// Запустить парсинг в режиме дополнения
    /// </summary>
    /// <param name="request">Запрос на дополнение</param>
    /// <returns>Результат операции</returns>
    [HttpPost("supplement")]
    public async Task<ActionResult<OperationResult<ParseResult>>> StartSupplement(
        [FromBody] SupplementRequest request
    )
    {
        ActionResult<OperationResult<ParseResult>> result = null!;

        try
        {
            // Получаем сервис фреймдаты через DI
            var framedataService =
                HttpContext.RequestServices.GetRequiredService<Tekken8FrameData>();

            var options = new FramedataParserOptions
            {
                RequestDelaySeconds = request.RequestDelaySeconds ?? 2,
                CharacterDelaySeconds = request.CharacterDelaySeconds ?? 5,
                UseStagingService = request.UseStagingService ?? true,
                ParseMoves = request.ParseMoves ?? true,
                IsSupplementMode = true, // Обязательно включаем режим дополнения
                MaxRetries = request.MaxRetries ?? 3,
                HttpTimeoutSeconds = request.HttpTimeoutSeconds ?? 30,
            };

            // Запускаем дополнение в фоновом режиме
            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    await framedataService.SupplementWithCustomOptions(request.Source, options);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка при дополнении фреймдаты");
                }
            });

            var parseResult = new ParseResult
            {
                Success = true,
                ParsedCharacters = [],
                Message = $"Запущено дополнение фреймдаты из {request.Source}",
            };

            result = Ok(OperationResult<ParseResult>.Ok("Дополнение запущено", parseResult));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запуске дополнения фреймдаты");
            var errorResult = new ParseResult
            {
                Success = false,
                Message = $"Ошибка при запуске дополнения: {ex.Message}",
            };
            result = Ok(
                OperationResult<ParseResult>.Bad("Ошибка при запуске дополнения", errorResult)
            );
        }

        return result;
    }

    /// <summary>
    /// Запустить дополнение фреймдаты с настройками по умолчанию
    /// </summary>
    /// <param name="source">Источник данных для дополнения</param>
    /// <returns>Результат операции</returns>
    [HttpPost("supplement/{source}")]
    public async Task<ActionResult<OperationResult<ParseResult>>> StartDefaultSupplement(
        FramedataSource source
    )
    {
        ActionResult<OperationResult<ParseResult>> result = null!;

        try
        {
            // Получаем сервис фреймдаты через DI
            var framedataService =
                HttpContext.RequestServices.GetRequiredService<Tekken8FrameData>();

            // Запускаем дополнение с настройками по умолчанию
            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    await framedataService.StartSupplementFrameData(null, null, source);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка при дополнении фреймдаты");
                }
            });

            var parseResult = new ParseResult
            {
                Success = true,
                ParsedCharacters = [],
                Message = $"Запущено дополнение фреймдаты из {source} с настройками по умолчанию",
            };

            result = Ok(
                OperationResult<ParseResult>.Ok(
                    "Дополнение с настройками по умолчанию запущено",
                    parseResult
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запуске дополнения фреймдаты");
            var errorResult = new ParseResult
            {
                Success = false,
                Message = $"Ошибка при запуске дополнения: {ex.Message}",
            };
            result = Ok(
                OperationResult<ParseResult>.Bad("Ошибка при запуске дополнения", errorResult)
            );
        }

        return result;
    }
}

/// <summary>
/// Запрос на парсинг фреймдаты
/// </summary>
public class ParseRequest
{
    /// <summary>
    /// Источник данных
    /// </summary>
    public FramedataSource Source { get; set; }

    /// <summary>
    /// Список имен персонажей для парсинга (null для всех)
    /// </summary>
    public List<string>? CharacterNames { get; set; }

    /// <summary>
    /// Задержка между запросами в секундах
    /// </summary>
    public int? RequestDelaySeconds { get; set; }

    /// <summary>
    /// Задержка между персонажами в секундах
    /// </summary>
    public int? CharacterDelaySeconds { get; set; }

    /// <summary>
    /// Использовать ли сервис ожидающих изменений
    /// </summary>
    public bool? UseStagingService { get; set; }

    /// <summary>
    /// Парсить ли мувы
    /// </summary>
    public bool? ParseMoves { get; set; }

    /// <summary>
    /// Максимальное количество попыток
    /// </summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Таймаут HTTP запросов в секундах
    /// </summary>
    public int? HttpTimeoutSeconds { get; set; }
}

/// <summary>
/// Запрос на дополнение фреймдаты
/// </summary>
public class SupplementRequest
{
    /// <summary>
    /// Источник данных для дополнения
    /// </summary>
    public FramedataSource Source { get; set; }

    /// <summary>
    /// Задержка между запросами в секундах
    /// </summary>
    public int? RequestDelaySeconds { get; set; }

    /// <summary>
    /// Задержка между персонажами в секундах
    /// </summary>
    public int? CharacterDelaySeconds { get; set; }

    /// <summary>
    /// Использовать ли сервис ожидающих изменений
    /// </summary>
    public bool? UseStagingService { get; set; }

    /// <summary>
    /// Парсить ли мувы
    /// </summary>
    public bool? ParseMoves { get; set; }

    /// <summary>
    /// Максимальное количество попыток
    /// </summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Таймаут HTTP запросов в секундах
    /// </summary>
    public int? HttpTimeoutSeconds { get; set; }
}

/// <summary>
/// Результат парсинга
/// </summary>
public class ParseResult
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Список распарсенных персонажей
    /// </summary>
    public List<string> ParsedCharacters { get; set; } = [];

    /// <summary>
    /// Сообщение о результате
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Результат с пагинацией
/// </summary>
public class PagedResult<T>
{
    /// <summary>
    /// Элементы текущей страницы
    /// </summary>
    public List<T> Items { get; set; } = [];

    /// <summary>
    /// Текущая страница
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Размер страницы
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Общее количество элементов
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Общее количество страниц
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Есть ли следующая страница
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Есть ли предыдущая страница
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
