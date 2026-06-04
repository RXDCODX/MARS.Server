using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.XPath;
using MARS.Server.Services.Framedata.Subservices.Entitys;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MARS.Server.Services.Framedata.Subservices.HtmlParsers;

/// <summary>
/// Парсер фреймдаты для сайта Wavu
/// </summary>
public class WavuFramedataParser : BaseFramedataParser
{
    private readonly Uri _basePath = new("https://wavu.wiki");
    private readonly HttpClient _httpClient;

    public WavuFramedataParser(
        ILogger logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        FramedataStagingService? stagingService,
        CancellationToken cancellationToken,
        FramedataParserOptions? options = null
    )
        : base(logger, dbContextFactory, stagingService, cancellationToken, options)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Options.HttpTimeoutSeconds) };
    }

    public override async Task<List<string>> ParseCharactersAndMoves(
        List<string>? characterNamesToParse = null
    )
    {
        var parsedCharacters = new List<string>();
        var config = AngleSharp.Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);

        var doc = await context.OpenAsync(_basePath.AbsoluteUri + "t/Main_Page", CancellationToken);

        var charSelectContainer = doc.QuerySelector("div.char-select-t8");
        if (charSelectContainer == null)
        {
            Logger.LogWarning("Не удалось найти контейнер выбора персонажей на Wavu");
            return parsedCharacters;
        }

        var charDivs = charSelectContainer.QuerySelectorAll("div.char-select-t8-img");

        // Скачиваем спрайт-лист один раз
        const string spriteUrl = "https://wavu.wiki/w/images/5/55/T8-spritesheet.webp";
        byte[]? spriteBytes = null;

        try
        {
            spriteBytes = await _httpClient.GetByteArrayAsync(spriteUrl, CancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Не удалось загрузить спрайт-лист с Wavu");
        }

        foreach (var charDiv in charDivs)
        {
            await DelayBetweenRequests();

            var nameNode = charDiv.ParentElement?.QuerySelector("div.char-select-t8-text > a");
            var name = nameNode?.TextContent.Trim().ToLower();

            if (
                string.IsNullOrWhiteSpace(name)
                || name.Equals("mokujin", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            name = name.Equals("jack-8") ? "jack 8" : name;

            // Проверяем, нужно ли парсить этого персонажа
            if (characterNamesToParse != null && !characterNamesToParse.Contains(name))
            {
                continue;
            }

            try
            {
                var character = await ParseCharacter(charDiv, doc, spriteBytes);

                if (Options.ParseMoves)
                {
                    var movelist = await GetMoveList(character);
                    var sortedMovelist = await ConsolidateMoveGroups(movelist);
                    await SaveCharacterAndMoves(character, sortedMovelist);
                }
                else
                {
                    await SaveCharacter(character);
                }

                parsedCharacters.Add(name);
                Logger.LogInformation("Успешно распарсил персонажа: {CharacterName}", name);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Ошибка при парсинге персонажа {CharacterName}", name);
            }
        }

        return parsedCharacters;
    }

    public override async Task<List<string>> ParseCharactersOnly(
        List<string>? characterNamesToParse = null
    )
    {
        var originalParseMoves = Options.ParseMoves;
        Options.ParseMoves = false;

        try
        {
            return await ParseCharactersAndMoves(characterNamesToParse);
        }
        finally
        {
            Options.ParseMoves = originalParseMoves;
        }
    }

    public override async Task<List<Move>> GetMoveList(TekkenCharacter character)
    {
        var movelist = new List<Move>();
        var config = AngleSharp.Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);

        var cargoQuery = _basePath.AbsoluteUri + GenerateCargoQueryUrl(character.Name);
        var doc = await context.OpenAsync(cargoQuery, CancellationToken);

        var tableNode = doc.QuerySelector("table.cargoTable > tbody");
        var rowNodes = tableNode?.QuerySelectorAll("tr");

        if (rowNodes == null)
        {
            Logger.LogWarning(
                "Не удалось найти таблицу мувов для персонажа {CharacterName}",
                character.Name
            );
            return movelist;
        }

        foreach (var rowNode in rowNodes)
        {
            var cellNodes = rowNode.QuerySelectorAll("td[class]");

            if (cellNodes is not { Length: >= 9 })
            {
                continue;
            }

            var command = cellNodes[0].TextContent.Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            var move = new Move
            {
                Character = character,
                CharacterName = character.Name,
                Command = command.Split('-').Last().Trim().ToLower().Replace(".", " "),
                StartUpFrame = cellNodes[1].TextContent.Trim().ToLower(),
                HitLevel = cellNodes[2].TextContent.Trim().ToLower(),
                Damage = cellNodes[3].TextContent.Trim().ToLower(),
                BlockFrame = cellNodes[4].TextContent.Trim().ToLower(),
                HitFrame = cellNodes[5].TextContent.Trim().ToLower(),
                CounterHitFrame = cellNodes[6].TextContent.Trim().ToLower(),
            };

            // Парсим Notes
            var notesList = new List<string>();
            var notesElement = cellNodes[8];

            // Рекурсивно собираем все элементы с текстом в отдельные строки
            var allTextElements = ExtractAllTextElements(notesElement);
            foreach (var textElement in allTextElements)
            {
                if (!string.IsNullOrWhiteSpace(textElement))
                {
                    notesList.Add(textElement.ToLower());
                }
            }

            move.Notes = notesList.Count > 0 ? [.. notesList] : null;

            // Парсим свойства мува
            ParseMoveProperties(move);

            movelist.Add(move);
        }

        return movelist;
    }

    private async Task<TekkenCharacter> ParseCharacter(
        IElement charDiv,
        IDocument doc,
        byte[]? spriteBytes
    )
    {
        var aNode = charDiv.QuerySelector("a");
        var href = aNode?.GetAttribute("href");
        var charPagePath = _basePath.AbsoluteUri + href;

        if (string.IsNullOrWhiteSpace(href))
        {
            throw new InvalidOperationException("Не удалось получить ссылку на страницу персонажа");
        }

        var charPage = await BrowsingContext
            .New(AngleSharp.Configuration.Default.WithDefaultLoader())
            .OpenAsync(charPagePath, CancellationToken);

        var divOutput =
            charPage.Body.SelectSingleNode("//*[@id=\"mw-content-text\"]/div[1]")
            ?? throw new Exception("Не удалось найти основной контент страницы");

        var divOutputElement =
            divOutput as IElement ?? throw new Exception("divOutput не является IElement");

        // Парсим описание
        var description = ParseDescription(divOutputElement);

        // Парсим сильные и слабые стороны
        var (strengths, weaknesses) = ParseStrengthsAndWeaknesses(divOutputElement);

        // Парсим изображение
        var (imageBytes, imageExtension) = await ParseCharacterImage(charDiv, doc, spriteBytes);

        var nameNode = charDiv.ParentElement?.QuerySelector("div.char-select-t8-text > a");
        var name = nameNode?.TextContent.Trim().ToLower();
        name = name?.Equals("jack-8") ?? false ? "jack 8" : name;

        return new TekkenCharacter
        {
            Name = name ?? throw new InvalidOperationException("Не удалось получить имя персонажа"),
            Description = description,
            Weaknesess = weaknesses,
            Strengths = strengths,
            Image = imageBytes,
            ImageExtension = imageExtension,
            PageUrl = charPagePath,
        };
    }

    private static string ParseDescription(IElement divOutputElement)
    {
        var pS =
            divOutputElement.QuerySelectorAll("p")
            ?? throw new Exception("Не удалось найти параграфы");
        var stringBuilder = new StringBuilder();

        foreach (var nodeb in pS)
        {
            if (!nodeb.TextContent.Contains("This page is"))
            {
                stringBuilder.Append(nodeb.TextContent);
            }
        }

        return stringBuilder.ToString();
    }

    private static (string[] strengths, string[] weaknesses) ParseStrengthsAndWeaknesses(
        IElement divOutputElement
    )
    {
        var federa =
            divOutputElement.SelectSingleNode(".//div[contains(@style, 'display: grid;')]")
            ?? throw new Exception("Не удалось найти блок с сильными и слабыми сторонами");

        var federaElement =
            federa as IElement ?? throw new Exception("federa не является IElement");

        var strAndWkns =
            federaElement.QuerySelectorAll("ul") ?? throw new Exception("Не удалось найти списки");
        var listStr = new List<string>();
        var listWknss = new List<string>();

        for (var index = 0; index < strAndWkns.Length; index++)
        {
            var za = strAndWkns[index];
            var twfs =
                za.QuerySelectorAll("li")
                ?? throw new Exception("Не удалось найти элементы списка");

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
                        throw new Exception("Неожиданный индекс списка");
                }
            }
        }

        return (listStr.ToArray(), listWknss.ToArray());
    }

    private async Task<(byte[]? imageBytes, string? imageExtension)> ParseCharacterImage(
        IElement charDiv,
        IDocument doc,
        byte[]? spriteBytes
    )
    {
        if (spriteBytes == null)
        {
            return (null, null);
        }

        var classList = charDiv.ClassList;
        var charClass = classList.FirstOrDefault(c => c != "char-select-t8-img");

        if (string.IsNullOrWhiteSpace(charClass))
        {
            return (null, null);
        }

        var (x, y, width, height) = ParseSpritePosition(doc, charClass, charDiv);

        if (x == 0 && y == 0)
        {
            return (null, null);
        }

        try
        {
            using var image = Image.Load<Rgba32>(spriteBytes);
            var spriteWidth = image.Width;
            var spriteHeight = image.Height;

            // Корректируем координаты и размеры
            x = Math.Abs(x);
            y = Math.Abs(y);

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
                Logger.LogWarning(
                    "Некорректные размеры обрезки: x={X}, y={Y}, width={Width}, height={Height}",
                    x,
                    y,
                    width,
                    height
                );
                return (null, null);
            }

            using var cropped = image.Clone(ctx => ctx.Crop(new Rectangle(x, y, width, height)));
            await using var croppedMs = new MemoryStream();
            await cropped.SaveAsPngAsync(croppedMs, CancellationToken);

            return (croppedMs.ToArray(), ".png");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка при обработке изображения персонажа");
            return (null, null);
        }
    }

    private static (int x, int y, int width, int height) ParseSpritePosition(
        IDocument doc,
        string charClass,
        IElement charDiv
    )
    {
        var x = 0;
        var y = 0;
        var width = 72;
        var height = 88;

        // Ищем CSS стили для позиционирования
        var styleNodes = doc.QuerySelectorAll("style[data-mw-deduplicate]");
        foreach (var styleNode in styleNodes)
        {
            var css = styleNode.TextContent;
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

        // Получаем размеры из img
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

        return (x, y, width, height);
    }

    private static void ParseMoveProperties(Move move)
    {
        var notes = move.Notes != null ? string.Join(" ", move.Notes) : string.Empty;

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

        if (move.HitLevel != null)
        {
            if (move.HitLevel.Contains("th") || move.HitLevel.ToLower().Contains('t'))
            {
                move.Throw = true;
            }
        }

        var pair = Aliases.Stances.FirstOrDefault(
            e => move.Command.StartsWith(e.Key, StringComparison.OrdinalIgnoreCase),
            new KeyValuePair<string, string>(string.Empty, string.Empty)
        );

        if (!string.IsNullOrWhiteSpace(pair.Key))
        {
            move.StanceCode = pair.Key;
            move.StanceName = pair.Value;
        }
    }

    private static string GenerateCargoQueryUrl(string characterName)
    {
        // Кодируем имя персонажа для URL
        characterName = characterName.Equals("jack 8", StringComparison.OrdinalIgnoreCase)
            ? "Jack-8"
            : characterName;
        var charName = string.Join(
            ' ',
            characterName
                .Split(' ')
                .Select(e => string.Concat(e[0].ToString().ToUpper(), e.AsSpan(1)))
        );
        var encodedName = Uri.EscapeDataString(charName + " movelist");

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

    /// <summary>
    /// Рекурсивно извлекает все элементы с текстом в отдельные строки
    /// </summary>
    /// <param name="element">Элемент для извлечения текста</param>
    /// <returns>Массив строк с текстом</returns>
    private static List<string> ExtractAllTextElements(IElement? element)
    {
        var textElements = new List<string>();

        if (element == null)
        {
            return textElements;
        }

        // Проверяем, есть ли у элемента прямые текстовые узлы (не в дочерних элементах)
        var hasDirectText = false;
        foreach (var node in element.ChildNodes)
        {
            if (node.NodeType == NodeType.Text)
            {
                var text = node.TextContent?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    hasDirectText = true;
                    textElements.Add(text);
                }
            }
        }

        // Если у элемента нет прямого текста, но есть дочерние элементы,
        // то рекурсивно обрабатываем их
        if (!hasDirectText)
        {
            foreach (var child in element.Children)
            {
                var childTextElements = ExtractAllTextElements(child);
                textElements.AddRange(childTextElements);
            }
        }

        return textElements;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
