using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.XPath;
using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Subservices.Entitys;

namespace MARS.Server.Services.Framedata.Subservices.HtmlParsers;

/// <summary>
/// Парсер фреймдаты для сайта Tekkendocs
/// </summary>
public class TekkendocsFramedataParser : BaseFramedataParser
{
    private readonly Uri _basePath = new("https://tekkendocs.com");
    private readonly HttpClient _httpClient;
    private readonly HtmlParser _htmlParser;

    public TekkendocsFramedataParser(
        ILogger logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        FramedataStagingService? stagingService,
        CancellationToken cancellationToken,
        FramedataParserOptions? options = null
    )
        : base(logger, dbContextFactory, stagingService, cancellationToken, options)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Options.HttpTimeoutSeconds) };
        _htmlParser = new HtmlParser();
    }

    public override async Task<List<string>> ParseCharactersAndMoves(
        List<string>? characterNamesToParse = null
    )
    {
        var parsedCharacters = new List<string>();
        var html = await _httpClient.GetStringAsync(_basePath.AbsoluteUri, CancellationToken);
        var doc = await _htmlParser.ParseDocumentAsync(html, CancellationToken);

        var ulNode = doc.Body?.SelectSingleNode("//ul") as IElement;
        var liNodes = ulNode
            ?.SelectNodes(".//li[@class='cursor-pointer']")
            ?.Cast<IElement>()
            .ToList();

        if (liNodes == null)
        {
            Logger.LogWarning("Не удалось найти список персонажей на Tekkendocs");
            return parsedCharacters;
        }

        foreach (var liNode in liNodes)
        {
            await DelayBetweenRequests();

            var nameNode =
                liNode.SelectSingleNode(".//div[contains(@class, 'text-center')]") as IElement;
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
                var character = await ParseCharacter(liNode);

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
        var chatPage = character.PageUrl;

        try
        {
            var html = await _httpClient.GetStringAsync(chatPage, CancellationToken);
            var doc = await _htmlParser.ParseDocumentAsync(html, CancellationToken);
            var tableNode = doc.Body?.SelectSingleNode("//tbody") as IElement;
            var rowNodes = tableNode
                ?.SelectNodes(".//tr[@class='rt-TableRow']")
                ?.Cast<IElement>()
                .ToList();

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
                var cellNodes = rowNode
                    ?.SelectNodes(".//td[@class='rt-TableCell']")
                    ?.Cast<IElement>()
                    .ToList();

                if (cellNodes == null || cellNodes.Count < 8)
                {
                    continue;
                }

                var command = (cellNodes[0].SelectSingleNode(".//a") as IElement)
                    ?.TextContent.Trim()
                    .ToLower();
                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                var move = new Move
                {
                    Character = character,
                    CharacterName = character.Name,
                    Command = command.Replace(".", " "),
                    HitLevel = cellNodes[1].TextContent.Trim().ToLower(),
                    Damage = cellNodes[2].TextContent.Trim().ToLower(),
                    StartUpFrame = cellNodes[3].TextContent.Trim().ToLower(),
                    BlockFrame = cellNodes[4].TextContent.Trim().ToLower(),
                    HitFrame = cellNodes[5].TextContent.Trim().ToLower(),
                    CounterHitFrame = cellNodes[6].TextContent.Trim().ToLower(),
                };

                // Парсим Notes
                var notesList = new List<string>();
                var notesElement = cellNodes[7];

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
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Ошибка при получении мувлиста для персонажа {CharacterName}",
                character.Name
            );
        }

        return movelist;
    }

    private Task<TekkenCharacter> ParseCharacter(IElement liNode)
    {
        var aNode = liNode.SelectSingleNode(".//a[@class='cursor-pointer']") as IElement;
        var href = aNode?.GetAttribute("href") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(href))
        {
            throw new InvalidOperationException("Не удалось получить ссылку на страницу персонажа");
        }

        href = href.StartsWith('/') ? href.Substring(1) : href;

        var imgNode = liNode.SelectSingleNode(".//img") as IElement;
        var imageUrl = imgNode?.GetAttribute("src") ?? "";
        var imagePath = new Uri(_basePath, imageUrl);

        var nameNode =
            liNode.SelectSingleNode(".//div[contains(@class, 'text-center')]") as IElement;
        var name = nameNode?.TextContent.Trim().ToLower();
        name = name?.Equals("jack-8") ?? false ? "jack 8" : name;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Не удалось получить имя персонажа");
        }

        var chatPage = _basePath.AbsoluteUri + href;

        return Task.FromResult(
            new TekkenCharacter
            {
                LinkToImage = imagePath.AbsoluteUri,
                Name = name,
                PageUrl = chatPage,
            }
        );
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
            if (move.HitLevel.Contains("th") || move.HitLevel.Contains('t'))
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

    /// <summary>
    /// Рекурсивно извлекает все элементы с текстом в отдельные строки
    /// </summary>
    /// <param name="node">Узел для извлечения текста</param>
    /// <returns>Массив строк с текстом</returns>
    private static List<string> ExtractAllTextElements(IElement node)
    {
        var textElements = new List<string>();

        if (node == null)
        {
            return textElements;
        }

        // Проверяем, есть ли у узла прямые текстовые узлы (не в дочерних элементах)
        var hasDirectText = false;
        foreach (var childNode in node.ChildNodes)
        {
            if (childNode.NodeType == NodeType.Text)
            {
                var text = childNode.TextContent?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    hasDirectText = true;
                    textElements.Add(text);
                }
            }
        }

        // Если у узла нет прямого текста, но есть дочерние элементы,
        // то рекурсивно обрабатываем их
        if (!hasDirectText)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == NodeType.Element && child is IElement element)
                {
                    var childTextElements = ExtractAllTextElements(element);
                    textElements.AddRange(childTextElements);
                }
            }
        }

        return textElements;
    }
}
