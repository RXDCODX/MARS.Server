using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.XPath;
using MARS.Server.Services.Framedata.Entitys;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MARS.Server.Services.Framedata;

/// <summary>
/// Provides methods for scraping and processing Tekken 8 frame data from web sources and updating the database.
/// </summary>
public partial class Tekken8FrameData
{
    internal async Task StartScrupFrameData(Chat? chat = default)
    {
        var config = AngleSharp.Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);

        var doc = await context.OpenAsync(BasePath.AbsoluteUri + "t/Main_Page", _cancellationToken);

        // Найти контейнер выбора персонажа
        var charSelectContainer = doc.QuerySelector("div.char-select-t8");

        var parsedCharacters = new List<string>(); // <--- Новый лист для успешно спарсенных

        if (charSelectContainer != null)
        {
            // Все div с персонажами
            var charDivs = charSelectContainer.QuerySelectorAll("div.char-select-t8-img");

            // --- Скачать спрайт-лист один раз ---
            const string spriteUrl = "https://wavu.wiki/w/images/5/55/T8-spritesheet.webp";
            byte[]? spriteBytes = null;
            var fileExtension = Path.GetExtension(spriteUrl);
            using var httpClient = new HttpClient();
            spriteBytes = await httpClient.GetByteArrayAsync(spriteUrl, _cancellationToken);

            foreach (var charDiv in charDivs)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _cancellationToken);

                // Получить ссылку на страницу персонажа
                var aNode = charDiv.QuerySelector("a");
                var href = aNode?.GetAttribute("href");
                var charPagePath = BasePath.AbsoluteUri + href;
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var charPage = await context.OpenAsync(charPagePath, _cancellationToken);

                var divOutput =
                    charPage.Body.SelectSingleNode("//*[@id=\"mw-content-text\"]/div[1]")
                    ?? throw new Exception("miss");
                var divOutputElement =
                    divOutput as IElement ?? throw new Exception("divOutput is not IElement");
                var pS = divOutputElement.QuerySelectorAll("p") ?? throw new Exception("miss");
                var stringBuilder = new StringBuilder();
                foreach (var nodeb in pS)
                {
                    if (!nodeb.TextContent.Contains("This page is"))
                    {
                        stringBuilder.Append(nodeb.TextContent);
                    }
                }
                var description = stringBuilder.ToString();
                stringBuilder.Clear();

                var federa =
                    divOutputElement.SelectSingleNode(".//div[contains(@style, 'display: grid;')]")
                    ?? throw new Exception("miss");
                var federaElement =
                    federa as IElement ?? throw new Exception("federa is not IElement");
                var strAndWkns =
                    federaElement.QuerySelectorAll("ul") ?? throw new Exception("miss");
                var listStr = new List<string>();
                var listWknss = new List<string>();

                for (var index = 0 ; index < strAndWkns.Length ; index++)
                {
                    var za = strAndWkns[index];
                    var twfs = za.QuerySelectorAll("li") ?? throw new Exception("miss");
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
                            default:
                                throw new Exception("sosal?");
                        }
                    }
                }

                // Имя персонажа
                var nameNode = charDiv.ParentElement?.QuerySelector("div.char-select-t8-text > a");
                var name = nameNode?.TextContent.Trim().ToLower();

                if (name?.Equals("mokujin", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    continue;
                }

                if (name == null)
                {
                    continue;
                }

                // --- Получение изображения персонажа ---
                // Получаем имя класса персонажа (например, alisa)
                var classList = charDiv.ClassList;
                var charClass = classList.FirstOrDefault(c => c != "char-select-t8-img");
                var x = 0;
                var y = 0;
                if (!string.IsNullOrWhiteSpace(charClass))
                {
                    // Ищем <style data-mw-deduplicate>
                    var styleNodes = doc.QuerySelectorAll("style[data-mw-deduplicate]");
                    foreach (var styleNode in styleNodes)
                    {
                        var css = styleNode.TextContent;
                        // Пример: .mw-parser-output .char-select-t8-img.alisa img { background-position: -1px -1px!important }
                        var pattern =
                            $@".mw-parser-output\s*.char-select-t8-img.{charClass}\s*img\s*\{{[^}}]*?background-position:\s*(-?\d+)px\s*(-?\d+)px";
                        var match = System.Text.RegularExpressions.Regex.Match(
                            css,
                            pattern,
                            System.Text.RegularExpressions.RegexOptions.Singleline
                        );
                        if (match.Success)
                        {
                            x = int.Parse(match.Groups[1].Value);
                            y = int.Parse(match.Groups[2].Value);
                            break;
                        }
                    }
                }
                // Если не нашли — x и y = 0

                if (x == 0 || y == 0)
                {
                    continue;
                }

                // Получаем width и height из img внутри charDiv
                int width = 72,
                    height = 88; // значения по умолчанию
                var imgNode = charDiv.QuerySelector("img");
                if (imgNode != null)
                {
                    var widthAttr = imgNode.GetAttribute("data-file-width");
                    var heightAttr = imgNode.GetAttribute("data-file-height");
                    if (!string.IsNullOrWhiteSpace(widthAttr))
                    {
                        width = int.Parse(widthAttr);
                    }

                    if (!string.IsNullOrWhiteSpace(heightAttr))
                    {
                        height = int.Parse(heightAttr);
                    }
                }
                byte[]? imageBytes = null;
                try
                {
                    using var image = Image.Load<Rgba32>(spriteBytes);
                    var spriteWidth = image.Width;
                    var spriteHeight = image.Height;

                    if (x < 0)
                    {
                        x = Math.Abs(x);
                    }

                    if (y < 0)
                    {
                        y = Math.Abs(y);
                    }

                    if (x + width > spriteWidth)
                    {
                        width = spriteWidth - x;
                    }

                    if (y + height > spriteHeight)
                    {
                        height = spriteHeight - y;
                    }

                    if (width <= 0 || height <= 0)
                    {
                        throw new Exception(
                            $"Некорректные размеры обрезки: x={x}, y={y}, width={width}, height={height}, spriteWidth={spriteWidth}, spriteHeight={spriteHeight}"
                        );
                    }

                    logger.LogInformation(
                        message: "Crop: x={x}, y={y}, width={width}, height={height}, spriteWidth={spriteWidth}, spriteHeight={spriteHeight}",
                        x,
                        y,
                        width,
                        height,
                        spriteWidth,
                        spriteHeight
                    );
                    using var cropped = image.Clone(ctx =>
                        ctx.Crop(new Rectangle(x, y, width, height))
                    );
                    await using var croppedMs = new MemoryStream();
                    await cropped.SaveAsPngAsync(croppedMs, _cancellationToken);
                    imageBytes = croppedMs.ToArray();
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                }

                var character = new TekkenCharacter
                {
                    Name = name,
                    Description = description,
                    Weaknesess = [.. listWknss],
                    Strengths = [.. listStr],
                    Image = imageBytes,
                    ImageExtension = fileExtension,
                    PageUrl = charPagePath,
                };

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
                    var cargoQuery = BasePath.AbsoluteUri + GenerateCargoQueryUrl(character.Name);
                    var movelist = await GetMoveList(character, cargoQuery);

                    var sortedMovelist = await ConsolidateMoveGroups(movelist);

                    await using AppDbContext dbContext =
                        await dbContextFactory.CreateDbContextAsync(_cancellationToken);

                    if (dbContext.TekkenCharacters.Any(e => e.Equals(character)))
                    {
                        dbContext.TekkenCharacters.Update(character);
                    }
                    else
                    {
                        dbContext.TekkenCharacters.Add(character);
                    }

                    foreach (Move move in sortedMovelist)
                    {
                        if (
                            dbContext.TekkenMoves.Any(e =>
                                e.CharacterName == move.CharacterName && e.Command == move.Command
                            )
                        )
                        {
                            dbContext.TekkenMoves.Update(move);
                        }
                        else
                        {
                            dbContext.TekkenMoves.Add(move);
                        }
                    }

                    var rowInt = await dbContext.SaveChangesAsync(_cancellationToken);

                    if (rowInt != sortedMovelist.Length + 1)
                    {
                        logger.LogCritical("Было обновленно не верное количетсво теккен ударов!");
                    }

                    // Добавляем имя в список успешно спарсенных
                    parsedCharacters.Add(name);
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                }
            }
        }

        // После основного парсинга — допарсить недостающих
        await ParseNotParsedCharacters(parsedCharacters);

        await UpdateMovesForVictorina();
        await client.SendMessage(
            chat switch
            {
                not null => chat,
                _ => TelegramExstension.Rxdcodx,
            },
            "Парсинг теккен фрейм даты закончено!",
            cancellationToken: _cancellationToken
        );
    }

    // Новый метод для допарсивания недостающих персонажей
    private async Task ParseNotParsedCharacters(List<string> parsedCharacters)
    {
        var allCharacterKeys = Aliases
            .CharacterNameAliases.Keys.Select(x => x.ToLower())
            .ToHashSet();
        var parsedSet = parsedCharacters.Select(x => x.ToLower()).ToHashSet();
        var missingCharacters = allCharacterKeys.Except(parsedSet).ToList();

        if (missingCharacters.Count > 0)
        {
            await StartScrupFrameDataFromSecondSiteForCharacters(missingCharacters);
        }
    }

    // Новый вспомогательный метод для парсинга только указанных персонажей через второй сайт
    private async Task StartScrupFrameDataFromSecondSiteForCharacters(
        List<string> characterNamesToParse
    )
    {
        var docW = new HtmlAgilityPack.HtmlWeb();
        var doc = await docW.LoadFromWebAsync(SecondBasePath.AbsoluteUri, _cancellationToken);

        var ulNode = doc.DocumentNode.SelectSingleNode("//ul");
        var liNodes = ulNode?.SelectNodes(".//li[@class='cursor-pointer']");

        if (liNodes != null)
        {
            foreach (var liNode in liNodes)
            {
                var aNode = liNode.SelectSingleNode(".//a[@class='cursor-pointer']");
                var href = aNode?.GetAttributeValue("href", string.Empty);

                var nameNode = liNode.SelectSingleNode(".//div[contains(@class, 'text-center')]");
                var name = nameNode?.InnerText.Trim().ToLower();

                if (name == null || !characterNamesToParse.Contains(name))
                {
                    continue;
                }

                if (name.Equals("mokujin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var imgNode = liNode.SelectSingleNode(".//img");
                var imageUrl = imgNode?.GetAttributeValue("src", "");
                var imagePath = new Uri(SecondBasePath, imageUrl);

                var chatPage = "https://tekkendocs.com" + href;

                var character = new TekkenCharacter
                {
                    LinkToImage = imagePath.AbsoluteUri,
                    Name = name,
                    PageUrl = chatPage,
                };

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
                    var movelist = await GetMoveListFromSecondSite(character, chatPage);
                    var sortedMovelist = await ConsolidateMoveGroups(movelist);

                    await using AppDbContext dbContext =
                        await dbContextFactory.CreateDbContextAsync(_cancellationToken);
                    foreach (Move move in sortedMovelist)
                    {
                        if (
                            dbContext.TekkenMoves.Any(e =>
                                e.CharacterName == move.CharacterName && e.Command == move.Command
                            )
                        )
                        {
                            dbContext.TekkenMoves.Update(move);
                        }
                        else
                        {
                            dbContext.TekkenMoves.Add(move);
                        }
                    }

                    if (dbContext.TekkenCharacters.Any(e => e.Equals(character)))
                    {
                        dbContext.TekkenCharacters.Update(character);
                    }
                    else
                    {
                        dbContext.TekkenCharacters.Add(character);
                    }

                    await dbContext.SaveChangesAsync(_cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                }
            }
        }
    }

    private static Task<Move[]> ConsolidateMoveGroups(List<Move> moves)
    {
        var groupedMoves = moves.GroupBy(m => new { m.CharacterName, m.Command });

        var consolidatedMoves = new List<Move>();
        var uniqueMoves = new List<Move>();

        foreach (var group in groupedMoves)
        {
            if (group.Count() > 1)
            {
                // Consolidate the duplicate moves
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
                    Notes = string.Join(Environment.NewLine, group.Select(m => m.Notes)),
                };

                consolidatedMoves.Add(consolidatedMove);
            }
            else
            // Add unique move to the list of unique moves
            {
                uniqueMoves.Add(group.First());
            }
        }

        // Combine unique moves and consolidated moves into an array
        var result = new Move[uniqueMoves.Count + consolidatedMoves.Count];
        uniqueMoves.CopyTo(result, 0);
        consolidatedMoves.CopyTo(result, uniqueMoves.Count);

        return Task.FromResult(result);
    }

    private async Task<List<Move>> GetMoveList(TekkenCharacter character, string url)
    {
        var movelist = new List<Move>();

        var config = AngleSharp.Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);
        var doc = await context.OpenAsync(url, _cancellationToken);

        // Находим таблицу с мувлистом
        var tableNode = doc.QuerySelector("table.cargoTable > tbody");

        // Проверяем, что таблица найдена
        var rowNodes = tableNode?.QuerySelectorAll("tr");

        // Проверяем, что строки таблицы найдены
        if (rowNodes == null)
        {
            return movelist;
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
                Character = character,
                CharacterName = character.Name,
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
            }

            if (move.HitLevel.Contains("th") || move.HitLevel.ToLower().Contains('t'))
            {
                move.Throw = true;
            }

            var pair = Aliases.Stances.FirstOrDefault(
                e => move.Command.StartsWith(e.Key, StringComparison.OrdinalIgnoreCase),
                DefaultValuePair
            );

            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                move.StanceCode = pair.Key;
                move.StanceName = pair.Value;
            }

            // Добавляем объект Move в список
            movelist.Add(move);
        }

        return movelist;
    }

    private static string GenerateCargoQueryUrl(string characterName)
    {
        // Кодируем имя персонажа для URL
        var encodedName = Uri.EscapeDataString(characterName + " movelist");

        // Формируем базовый URL запроса
        const string baseUrl = "/w/index.php?title=Special:CargoQuery";

        // Параметры запроса
        var queryParams = new Dictionary<string, string>
        {
            { "tables", "Move" },
            {
                "fields",
                "CONCAT(id,'')=Move,startup=Startup,target=Hit Level,damage=Damage,CONCAT(block,'')=On Block,CONCAT(hit,'')=On Hit,CONCAT(ch,'')=On CH,crush=States,notes=Notes"
            },
            {
                "where",
                $"Move._pageName='{string.Concat(encodedName[0].ToString().ToUpper(), encodedName.AsSpan(1))}'"
            },
            { "format", "table" },
            { "offset", "0" },
            { "limit", "500" }, // Увеличиваем лимит, чтобы получить все движения
        };

        // Собираем URL
        var queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));
        return $"{baseUrl}&{queryString}";
    }
}
