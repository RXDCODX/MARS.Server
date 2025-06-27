using System.Text;
using HtmlAgilityPack;
using MARS.Server.Services.Framedata.Entitys;

namespace MARS.Server.Services.Framedata;

public partial class Tekken8FrameData
{
    internal async Task StartScrupFrameData(Chat? chat = default)
    {
        var docW = new HtmlWeb();
        var doc = await docW.LoadFromWebAsync(
            BasePath.AbsoluteUri + "t/Main_Page",
            _cancellationToken
        );

        // Find the character selection container
        var charSelectContainer = doc.DocumentNode.SelectSingleNode(
            "//div[@class='char-select-t8']"
        );

        if (charSelectContainer != null)
        {
            // Get all character divs
            var charDivs = charSelectContainer.SelectNodes(
                ".//div[contains(@class, 'char-select-t8-img')]"
            );

            if (charDivs != null)
            {
                foreach (HtmlNode charDiv in charDivs)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), _cancellationToken);

                    // Get the link node
                    var aNode = charDiv.SelectSingleNode(".//a");
                    var href = aNode?.GetAttributeValue("href", string.Empty);

                    var charDoc = new HtmlWeb();
                    var duDoc = await charDoc.LoadFromWebAsync(
                        BasePath.AbsoluteUri + href,
                        _cancellationToken
                    );

                    var divOutput =
                        duDoc.DocumentNode.SelectSingleNode(
                            "/html/body/div[1]/main/div/div[2]/div[2]/div[1]"
                        ) ?? throw new HtmlWebException("miss");
                    var pS = divOutput.SelectNodes("./p") ?? throw new HtmlWebException("miss");

                    var stringBuilder = new StringBuilder();

                    foreach (var nodeb in pS)
                    {
                        stringBuilder.Append(nodeb.InnerText);
                    }

                    var description = stringBuilder.ToString();
                    stringBuilder.Clear();

                    var federa =
                        divOutput.SelectSingleNode("./div[contains(@style, 'display: grid;')]")
                        ?? throw new HtmlWebException("miss");
                    var strAndWkns =
                        federa.SelectNodes(".//ul") ?? throw new HtmlWebException("miss");

                    var listStr = new List<string>();
                    var listWknss = new List<string>();

                    for (var index = 0; index < strAndWkns.Count; index++)
                    {
                        HtmlNode? za = strAndWkns[index];
                        var twfs = za.SelectNodes("./li") ?? throw new HtmlWebException("miss");
                        foreach (var htmlNode in twfs)
                        {
                            var innerGrps = htmlNode.InnerText;
                            if (index == 0)
                            {
                                listStr.Add(innerGrps);
                            }
                            else if (index == 1)
                            {
                                listWknss.Add(innerGrps);
                            }
                            else
                            {
                                throw new HtmlWebException("sosal?");
                            }
                        }
                    }

                    // Get character name from the sibling div
                    var nameNode = charDiv.ParentNode.SelectSingleNode(
                        ".//div[@class='char-select-t8-text']/a"
                    );
                    var name = nameNode?.InnerText.Trim().ToLower();

                    if (name?.Equals("mokujin", StringComparison.OrdinalIgnoreCase) ?? false)
                    {
                        continue;
                    }

                    if (name != null)
                    {
                        var character = new TekkenCharacter
                        {
                            Name = name,
                            Description = description,
                            Weaknesess = listWknss.ToArray(),
                            Strengths = listStr.ToArray(),
                        };

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
                            var cargoQuery =
                                BasePath.AbsoluteUri + GenerateCargoQueryUrl(character.Name);
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
                                        e.CharacterName == move.CharacterName
                                        && e.Command == move.Command
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
                                logger.LogCritical(
                                    "Было обновленно не верное количетсво теккен ударов!"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogException(ex);
                        }
                    }
                }
            }
        }

        await UpdateMovesForVictorina();
        await client.SendMessage(
            chat is not null ? chat : TelegramExstension.Rxdcodx,
            "Парсинг теккен фрейм даты закончено!",
            cancellationToken: _cancellationToken
        );
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

        // Загрузка HTML страницы
        var web = new HtmlWeb();
        var doc = await web.LoadFromWebAsync(url, _cancellationToken);

        // Находим таблицу с мувлистом
        var tableNode = doc.DocumentNode.SelectSingleNode(
            "//table[contains(@class, 'cargoTable')]/tbody"
        );

        // Проверяем, что таблица найдена
        var rowNodes = tableNode?.SelectNodes(".//tr");

        // Проверяем, что строки таблицы найдены
        if (rowNodes != null)
        {
            foreach (var rowNode in rowNodes)
            {
                // Получаем ячейки (столбцы) текущей строки
                var cellNodes = rowNode.SelectNodes(".//td[@class]");

                if (cellNodes != null && cellNodes.Count >= 9) // Проверяем, что есть все нужные столбцы
                {
                    var command = cellNodes[0].InnerText.Trim();

                    // Создаем новый объект Move
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        var move = new Move
                        {
                            Character = character,
                            CharacterName = character.Name,
                            Command = command.Split('-').Last().Trim().ToLower(),
                        };

                        move.Command = move.Command.Replace(".", " ");

                        // Заполняем остальные свойства объекта Move данными из остальных ячеек
                        move.StartUpFrame = cellNodes[1].InnerText.Trim().ToLower();
                        move.HitLevel = cellNodes[2].InnerText.Trim().ToLower();
                        move.Damage = cellNodes[3].InnerText.Trim().ToLower();
                        move.BlockFrame = cellNodes[4].InnerText.Trim().ToLower();
                        move.HitFrame = cellNodes[5].InnerText.Trim().ToLower();
                        move.CounterHitFrame = cellNodes[6].InnerText.Trim().ToLower();
                        move.Notes = cellNodes[8].InnerText.Trim().ToLower();

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
                }
            }
        }

        return movelist;
    }

    private string GenerateCargoQueryUrl(string characterName)
    {
        // Кодируем имя персонажа для URL
        var encodedName = Uri.EscapeDataString(characterName + " movelist");

        // Формируем базовый URL запроса
        var baseUrl = "/w/index.php?title=Special:CargoQuery";

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
