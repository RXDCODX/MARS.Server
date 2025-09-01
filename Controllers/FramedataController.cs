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
    /// Получить всех персонажей
    /// </summary>
    /// <returns>Список всех персонажей</returns>
    [HttpGet("characters")]
    public async Task<ActionResult<IEnumerable<TekkenCharacter>>> GetCharacters()
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var characters = await dbContext
                .TekkenCharacters.Include(c => c.Movelist)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(characters);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении персонажей");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить персонажа по имени
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Персонаж</returns>
    [HttpGet("characters/{name}")]
    public async Task<ActionResult<TekkenCharacter>> GetCharacter(string name)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            var character = await dbContext
                .TekkenCharacters.Include(c => c.Movelist)
                .FirstOrDefaultAsync(c => c.Name == name);

            return character == null ? NotFound($"Персонаж '{name}' не найден") : Ok(character);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении персонажа {Name}", name);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Создать нового персонажа
    /// </summary>
    /// <param name="character">Данные персонажа</param>
    /// <returns>Созданный персонаж</returns>
    [HttpPost("characters")]
    public async Task<ActionResult<TekkenCharacter>> CreateCharacter(
        [FromBody] TekkenCharacter character
    )
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            if (await dbContext.TekkenCharacters.AnyAsync(c => c.Name == character.Name))
            {
                return BadRequest($"Персонаж с именем '{character.Name}' уже существует");
            }

            character.LastUpdateTime = DateTimeOffset.Now;
            dbContext.TekkenCharacters.Add(character);
            await dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCharacter), new { name = character.Name }, character);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании персонажа {Name}", character.Name);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Обновить персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <param name="character">Обновленные данные</param>
    /// <returns>Обновленный персонаж</returns>
    [HttpPut("characters/{name}")]
    public async Task<ActionResult<TekkenCharacter>> UpdateCharacter(
        string name,
        [FromBody] TekkenCharacter character
    )
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var existingCharacter = await dbContext
                .TekkenCharacters.Include(c => c.Movelist)
                .FirstOrDefaultAsync(c => c.Name == name);

            if (existingCharacter == null)
            {
                return NotFound($"Персонаж '{name}' не найден");
            }

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

            return Ok(existingCharacter);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении персонажа {Name}", name);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Удалить персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Результат удаления</returns>
    [HttpDelete("characters/{name}")]
    public async Task<ActionResult> DeleteCharacter(string name)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var character = await dbContext
                .TekkenCharacters.Include(c => c.Movelist)
                .FirstOrDefaultAsync(c => c.Name == name);

            if (character == null)
            {
                return NotFound($"Персонаж '{name}' не найден");
            }

            dbContext.TekkenCharacters.Remove(character);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении персонажа {Name}", name);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить движения персонажа
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <returns>Список движений</returns>
    [HttpGet("characters/{characterName}/moves")]
    public async Task<ActionResult<IEnumerable<Move>>> GetCharacterMoves(string characterName)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var moves = await dbContext
                .TekkenMoves.Where(m => m.CharacterName == characterName)
                .OrderBy(m => m.Command)
                .ToListAsync();

            return Ok(moves);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении движений персонажа {CharacterName}",
                characterName
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить конкретное движение
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="command">Команда движения</param>
    /// <returns>Движение</returns>
    [HttpGet("characters/{characterName}/moves/{command}")]
    public async Task<ActionResult<Move>> GetMove(string characterName, string command)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var move = await dbContext.TekkenMoves.FirstOrDefaultAsync(m =>
                m.CharacterName == characterName && m.Command == command
            );

            return move == null
                ? (ActionResult<Move>)
                    NotFound($"Движение '{command}' для персонажа '{characterName}' не найдено")
                : (ActionResult<Move>)Ok(move);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при получении движения {Command} для персонажа {CharacterName}",
                command,
                characterName
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Создать новое движение
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="move">Данные движения</param>
    /// <returns>Созданное движение</returns>
    [HttpPost("characters/{characterName}/moves")]
    public async Task<ActionResult<Move>> CreateMove(string characterName, [FromBody] Move move)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            // Проверяем существование персонажа
            var character = await dbContext.TekkenCharacters.FirstOrDefaultAsync(c =>
                c.Name == characterName
            );
            if (character == null)
            {
                return NotFound($"Персонаж '{characterName}' не найден");
            }

            // Проверяем существование движения
            if (
                await dbContext.TekkenMoves.AnyAsync(m =>
                    m.CharacterName == characterName && m.Command == move.Command
                )
            )
            {
                return BadRequest(
                    $"Движение '{move.Command}' для персонажа '{characterName}' уже существует"
                );
            }

            move.CharacterName = characterName;
            dbContext.TekkenMoves.Add(move);
            await dbContext.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetMove),
                new { characterName, command = move.Command },
                move
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при создании движения {Command} для персонажа {CharacterName}",
                move.Command,
                characterName
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Обновить движение
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="command">Команда движения</param>
    /// <param name="move">Обновленные данные движения</param>
    /// <returns>Обновленное движение</returns>
    [HttpPut("characters/{characterName}/moves/{command}")]
    public async Task<ActionResult<Move>> UpdateMove(
        string characterName,
        string command,
        [FromBody] Move move
    )
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var existingMove = await dbContext.TekkenMoves.FirstOrDefaultAsync(m =>
                m.CharacterName == characterName && m.Command == command
            );

            if (existingMove == null)
            {
                return NotFound($"Движение '{command}' для персонажа '{characterName}' не найдено");
            }

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

            return Ok(existingMove);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при обновлении движения {Command} для персонажа {CharacterName}",
                command,
                characterName
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Удалить движение
    /// </summary>
    /// <param name="characterName">Имя персонажа</param>
    /// <param name="command">Команда движения</param>
    /// <returns>Результат удаления</returns>
    [HttpDelete("characters/{characterName}/moves/{command}")]
    public async Task<ActionResult> DeleteMove(string characterName, string command)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var move = await dbContext.TekkenMoves.FirstOrDefaultAsync(m =>
                m.CharacterName == characterName && m.Command == command
            );

            if (move == null)
            {
                return NotFound($"Движение '{command}' для персонажа '{characterName}' не найдено");
            }

            dbContext.TekkenMoves.Remove(move);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при удалении движения {Command} для персонажа {CharacterName}",
                command,
                characterName
            );
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Поиск движений по фильтрам
    /// </summary>
    /// <param name="characterName">Имя персонажа (опционально)</param>
    /// <param name="stanceCode">Код стойки (опционально)</param>
    /// <param name="heatEngage">Требует ли Heat Engage (опционально)</param>
    /// <param name="powerCrush">Является ли Power Crush (опционально)</param>
    /// <param name="throw">Является ли броском (опционально)</param>
    /// <param name="homing">Является ли Homing (опционально)</param>
    /// <returns>Отфильтрованные движения</returns>
    [HttpGet("moves/search")]
    public async Task<ActionResult<IEnumerable<Move>>> SearchMoves(
        [FromQuery] string? characterName = null,
        [FromQuery] string? stanceCode = null,
        [FromQuery] bool? heatEngage = null,
        [FromQuery] bool? powerCrush = null,
        [FromQuery] bool? isThrow = null,
        [FromQuery] bool? homing = null
    )
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var query = dbContext.TekkenMoves.AsQueryable();

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

            var moves = await query
                .OrderBy(m => m.CharacterName)
                .ThenBy(m => m.Command)
                .ToListAsync();

            return Ok(moves);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при поиске движений");
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить изображение персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Изображение персонажа</returns>
    [HttpGet("characters/{name}/image")]
    public async Task<ActionResult> GetCharacterImage(string name)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var character = await dbContext.TekkenCharacters.FirstOrDefaultAsync(c =>
                c.Name == name
            );

            if (character == null)
            {
                return NotFound($"Персонаж '{name}' не найден");
            }

            if (character.Image == null || character.Image.Length == 0)
            {
                return NotFound($"Изображение для персонажа '{name}' не найдено");
            }

            var contentType = GetContentType(character.ImageExtension);
            return File(character.Image, contentType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении изображения персонажа {Name}", name);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить аватар персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Аватар персонажа</returns>
    [HttpGet("characters/{name}/avatar")]
    public async Task<ActionResult> GetCharacterAvatar(string name)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var character = await dbContext.TekkenCharacters.FirstOrDefaultAsync(c =>
                c.Name == name
            );

            if (character == null)
            {
                return NotFound($"Персонаж '{name}' не найден");
            }

            if (character.AvatarImage == null || character.AvatarImage.Length == 0)
            {
                return NotFound($"Аватар для персонажа '{name}' не найден");
            }

            var contentType = GetContentType(character.AvatarImageExtension);
            return File(character.AvatarImage, contentType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении аватара персонажа {Name}", name);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Получить полное изображение персонажа
    /// </summary>
    /// <param name="name">Имя персонажа</param>
    /// <returns>Полное изображение персонажа</returns>
    [HttpGet("characters/{name}/fullbody")]
    public async Task<ActionResult> GetCharacterFullBody(string name)
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var character = await dbContext.TekkenCharacters.FirstOrDefaultAsync(c =>
                c.Name == name
            );

            if (character == null)
            {
                return NotFound($"Персонаж '{name}' не найден");
            }

            if (character.FullBodyImage == null || character.FullBodyImage.Length == 0)
            {
                return NotFound($"Полное изображение для персонажа '{name}' не найдено");
            }

            var contentType = GetContentType(character.FullBodyImageExtension);
            return File(character.FullBodyImage, contentType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении полного изображения персонажа {Name}", name);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
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
    public async Task<ActionResult<ParseResult>> ParseFramedata([FromBody] ParseRequest request)
    {
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

            var result = await parser.ParseCharactersAndMoves(request.CharacterNames);

            return Ok(
                new ParseResult
                {
                    Success = true,
                    ParsedCharacters = result,
                    Message = $"Успешно распарсено {result.Count} персонажей",
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при парсинге фреймдаты");
            return StatusCode(
                500,
                new ParseResult { Success = false, Message = $"Ошибка при парсинге: {ex.Message}" }
            );
        }
    }

    /// <summary>
    /// Запустить парсинг только персонажей (без мувов)
    /// </summary>
    /// <param name="request">Запрос на парсинг</param>
    /// <returns>Результат парсинга</returns>
    [HttpPost("parse-characters-only")]
    public async Task<ActionResult<ParseResult>> ParseCharactersOnly(
        [FromBody] ParseRequest request
    )
    {
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

            var result = await parser.ParseCharactersOnly(request.CharacterNames);

            return Ok(
                new ParseResult
                {
                    Success = true,
                    ParsedCharacters = result,
                    Message = $"Успешно распарсено {result.Count} персонажей (без мувов)",
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при парсинге персонажей");
            return StatusCode(
                500,
                new ParseResult { Success = false, Message = $"Ошибка при парсинге: {ex.Message}" }
            );
        }
    }

    /// <summary>
    /// Запустить парсинг в режиме дополнения
    /// </summary>
    /// <param name="request">Запрос на дополнение</param>
    /// <returns>Результат операции</returns>
    [HttpPost("supplement")]
    public async Task<ActionResult<ParseResult>> StartSupplement(
        [FromBody] SupplementRequest request
    )
    {
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

            return Ok(
                new ParseResult
                {
                    Success = true,
                    ParsedCharacters = [],
                    Message = $"Запущено дополнение фреймдаты из {request.Source}",
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запуске дополнения фреймдаты");
            return StatusCode(
                500,
                new ParseResult
                {
                    Success = false,
                    Message = $"Ошибка при запуске дополнения: {ex.Message}",
                }
            );
        }
    }

    /// <summary>
    /// Запустить дополнение фреймдаты с настройками по умолчанию
    /// </summary>
    /// <param name="source">Источник данных для дополнения</param>
    /// <returns>Результат операции</returns>
    [HttpPost("supplement/{source}")]
    public async Task<ActionResult<ParseResult>> StartDefaultSupplement(FramedataSource source)
    {
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

            return Ok(
                new ParseResult
                {
                    Success = true,
                    ParsedCharacters = [],
                    Message =
                        $"Запущено дополнение фреймдаты из {source} с настройками по умолчанию",
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запуске дополнения фреймдаты");
            return StatusCode(
                500,
                new ParseResult
                {
                    Success = false,
                    Message = $"Ошибка при запуске дополнения: {ex.Message}",
                }
            );
        }
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
