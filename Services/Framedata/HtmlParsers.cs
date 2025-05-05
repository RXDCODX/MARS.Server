using HtmlAgilityPack;
using MARS.Server.Services.Framedata.Entitys;

namespace MARS.Server.Services.Framedata;

public partial class Tekken8FrameData
{
    internal async Task StartScrupFrameData(Chat? chat = default)
    {
        var docW = new HtmlWeb();
        var doc = await docW.LoadFromWebAsync(BasePath.AbsoluteUri, _cancellationToken);

        var ulNode = doc.DocumentNode.SelectSingleNode("//ul");

        var liNodes = ulNode?.SelectNodes(".//li[@class='cursor-pointer']");

        if (liNodes != null)
        {
            foreach (HtmlNode liNode in liNodes)
            {
                var aNode = liNode.SelectSingleNode(".//a[@class='cursor-pointer']");
                var href = aNode?.GetAttributeValue("href", string.Empty);

                var nameNode = liNode.SelectSingleNode(".//div[contains(@class, 'text-center')]");
                var name = nameNode?.InnerText.Trim();

                if (name?.Equals("mokujin", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    continue;
                }

                var imgNode = liNode.SelectSingleNode(".//img");
                var imageUrl = imgNode?.GetAttributeValue("src", "");
                var imagePath = new Uri(BasePath, imageUrl);

                if (name != null)
                {
                    var character = new TekkenCharacter
                    {
                        LinkToImage = imagePath.AbsoluteUri,
                        Name = name,
                    };

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
                        var movelist = await GetMoveList(
                            character,
                            "https://tekkendocs.com" + href
                        );

                        var sortedMovelist = await ConsolidateMoveGroups(movelist);

                        await using AppDbContext dbContext =
                            await dbContextFactory.CreateDbContextAsync(_cancellationToken);
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

        await UpdateMovesForVictorina();
        await client.SendMessage(
            chat is not null ? chat : TelegramExstension.Rxdcodx,
            "Парсинг теккен фрейм даты закончено!",
            cancellationToken: _cancellationToken
        );
    }

    public static Task<Move[]> ConsolidateMoveGroups(List<Move> moves)
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
        var tableNode = doc.DocumentNode.SelectSingleNode("//tbody");

        // Проверяем, что таблица найдена
        var rowNodes = tableNode?.SelectNodes(".//tr[@class='rt-TableRow']");

        // Проверяем, что строки таблицы найдены
        if (rowNodes != null)
        {
            foreach (var rowNode in rowNodes)
            {
                //character.Movelist = movelist; !important

                // Получаем ячейки (столбцы) текущей строки
                var cellNodes = rowNode.SelectNodes(".//td[@class='rt-TableCell']");

                // Извлекаем текст из тега <a> в ячейке command
                if (cellNodes != null)
                {
                    var command = cellNodes[0].SelectSingleNode(".//a")?.InnerText.Trim().ToLower();

                    // Создаем новый объект Move
                    if (command != null)
                    {
                        var move = new Move
                        {
                            Character = character,
                            CharacterName = character.Name,
                            Command = command,
                        };

                        if (string.IsNullOrWhiteSpace(move.Command))
                        {
                            continue;
                        }

                        move.Command = move.Command!.Replace(".", " ");

                        var noteDivs = cellNodes[7].SelectNodes(".//div");
                        if (noteDivs is { Count: > 0 })
                        {
                            move.Notes = string.Join(
                                Environment.NewLine,
                                noteDivs.Select(div => div.InnerText.Trim().ToLower())
                            );
                        }

                        // Заполняем остальные свойства объекта Move данными из остальных ячеек
                        move.HitLevel = cellNodes[1].InnerText.Trim().ToLower();
                        move.Damage = cellNodes[2].InnerText.Trim().ToLower();
                        move.StartUpFrame = cellNodes[3].InnerText.Trim().ToLower();
                        move.BlockFrame = cellNodes[4].InnerText.Trim().ToLower();
                        move.HitFrame = cellNodes[5].InnerText.Trim().ToLower();
                        move.CounterHitFrame = cellNodes[6].InnerText.Trim().ToLower();

                        var notes = move.Notes;
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

                        if (move.HitLevel.Contains("th") || move.HitLevel.Contains('t'))
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
}
