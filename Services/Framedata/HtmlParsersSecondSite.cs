using HtmlAgilityPack;
using MARS.Server.Services.Framedata.Entitys;

namespace MARS.Server.Services.Framedata;

public partial class Tekken8FrameData
{
    internal async Task StartScrupFrameDataFromSecondSite(Chat? chat = default)
    {
        var docW = new HtmlWeb();
        var doc = await docW.LoadFromWebAsync(SecondBasePath.AbsoluteUri, _cancellationToken);

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
                var imagePath = new Uri(SecondBasePath, imageUrl);

                if (name != null)
                {
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
    }

    // Удаляю дублирующуюся реализацию GetMoveListFromSecondSite, оставляю только HtmlAgilityPack версию
    private async Task<List<Move>> GetMoveListFromSecondSite(TekkenCharacter character, string url)
    {
        var movelist = new List<Move>();

        // Загрузка HTML страницы
        var web = new HtmlAgilityPack.HtmlWeb();
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
