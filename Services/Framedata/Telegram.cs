using System.Text;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.Framedata;

public partial class Tekken8FrameData
{
    public async Task HandAlert(ITelegramBotClient telegramClient, Update update)
    {
        if (update.Type == UpdateType.CallbackQuery)
        {
            var data = update.CallbackQuery;
            if (data is { Data: not null })
            {
                var split = data.Data.Split(':');

                if (split[0].Equals("framedata"))
                {
                    var charname = split[1];
                    var type = split[2];
                    var chatid = new ChatId(data.Message!.Chat.Id);

                    var movelist = await GetCharMoveListAsync(charname);

                    if (movelist is null)
                    {
                        var errorAnswer =
                            $"Ошибка парсинга ответного запроса, сообщите разработчику об ошибке. {data.Data}";
                        await telegramClient.SendMessage(
                            chatid,
                            errorAnswer,
                            cancellationToken: _cancellationToken
                        );
                        return;
                    }

                    var text = new StringBuilder();
                    switch (type)
                    {
                        case "homing":
                            text.AppendLine("<b>Homings</b>");
                            text.AppendLine();

                            text.AppendJoin(
                                Environment.NewLine,
                                movelist.Where(e => e.Homing).Select(e => e.Command)
                            );
                            break;
                        case "heatengage":
                            text.AppendLine("<b>Heat Engagers</b>");
                            text.AppendLine();

                            text.AppendJoin(
                                Environment.NewLine,
                                movelist.Where(e => e.HeatEngage).Select(e => e.Command)
                            );
                            break;
                        case "tornado":
                            text.AppendLine("<b>Tornados</b>");
                            text.AppendLine();

                            text.AppendJoin(
                                Environment.NewLine,
                                movelist.Where(e => e.Tornado).Select(e => e.Command)
                            );
                            break;
                        case "heatsmash":
                            text.AppendLine("<b>Heat Smashes</b>");
                            text.AppendLine();

                            text.AppendJoin(
                                Environment.NewLine,
                                movelist.Where(e => e.HeatSmash).Select(e => e.Command)
                            );
                            break;
                        case "heatburst":
                            text.AppendLine("<b>Heat Bursts</b>");
                            text.AppendLine();

                            text.AppendJoin(
                                Environment.NewLine,
                                movelist.Where(e => e.HeatBurst).Select(e => e.Command)
                            );
                            break;
                        case "powercrush":
                            text.AppendLine("<b>Power Crushes</b>");
                            text.AppendLine();
                            text.AppendJoin(
                                Environment.NewLine,
                                movelist.Where(e => e.PowerCrush).Select(e => e.Command)
                            );
                            break;
                        case "stance":
                            var stanceCode = split[3];
                            KeyValuePair<string, string>? pair = Aliases.Stances.FirstOrDefault(e =>
                                e.Key == stanceCode
                            );
                            text.AppendLine($"<b>{pair.Value}</b>");
                            text.AppendLine();
                            text.AppendJoin(
                                Environment.NewLine,
                                movelist!
                                    .Where(e =>
                                        e.StanceCode.Equals(
                                            stanceCode,
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                    )
                                    .Select(e => e.Command)
                            );
                            break;
                        case "throw":
                            text.AppendLine("<b>Throws</b>");
                            text.AppendLine();
                            text.AppendJoin(
                                Environment.NewLine,
                                movelist!.Where(e => e.Throw).Select(e => e.Command)
                            );
                            break;
                    }

                    await telegramClient.AnswerCallbackQuery(
                        data.Id,
                        cancellationToken: _cancellationToken
                    );
                    await telegramClient.SendMessage(
                        chatid,
                        text.ToString(),
                        ParseMode.Html,
                        cancellationToken: _cancellationToken
                    );
                }
            }
        }
    }
}
