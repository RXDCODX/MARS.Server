using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnFramedataCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var msg = message.Text;
        var text = string.Empty;
        var split = msg?.Split(" ");

        if (split is { Length: >= 3 })
        {
            var keyWords = split.Skip(1).ToArray();

            var move = await frameData.GetMoveAsync(keyWords).ConfigureAwait(false);

            if (move != null)
            {
                if (move.Character != null)
                {
                    var url = move.Character.LinkToImage;

                    text = $"""
                        🎭 <b>Character</b> 🎭
                                <i>{move.Character.Name}</i>

                        ///////////////////
                        🔡 <b>Input</b> 🔡
                                <i>{move.Command}</i>

                        🚀 <b>Startup</b> 🚀
                                <i>{move.StartUpFrame}</i>

                        🏁 <b>Block frame</b> 🏁
                                <i>{move.BlockFrame}</i>

                        🎯 <b>Hit frame</b> 🎯
                                <i>{move.HitFrame}</i>

                        🤝 <b>Counter hit frame</b> 🤝
                                <i>{move.CounterHitFrame}</i>

                        ///////////////////
                        📊 <b>Hit Level</b> 📊
                                <i>{move.HitLevel}</i>

                        💥 <b>Damage</b> 💥
                                <i>{move.Damage}</i>

                        📝 <b>Notes</b> 📝

                        <i>{move.Notes}</i>
                        
                        """;

                    var buttons = new List<InlineKeyboardButton>();

                    if (move.HeatEngage)
                    {
                        var button = new InlineKeyboardButton("Heat Engager")
                        {
                            CallbackData = $"framedata:{move.Character.Name}:heatengage",
                        };
                        buttons.Add(button);
                    }

                    if (move.Tornado)
                    {
                        var button = new InlineKeyboardButton("Tornado")
                        {
                            CallbackData = $"framedata:{move.Character.Name}:tornado",
                        };
                        buttons.Add(button);
                    }

                    if (move.HeatSmash)
                    {
                        var button = new InlineKeyboardButton("Heat Smash")
                        {
                            CallbackData = $"framedata:{move.Character.Name}:heatsmash",
                        };
                        buttons.Add(button);
                    }

                    if (move.PowerCrush)
                    {
                        var button = new InlineKeyboardButton("Power Crush")
                        {
                            CallbackData = $"framedata:{move.Character.Name}:powercrush",
                        };
                        buttons.Add(button);
                    }

                    if (move.HeatBurst)
                    {
                        var button = new InlineKeyboardButton("Heat Burst")
                        {
                            CallbackData = $"framedata:{move.Character.Name}:heatburst",
                        };
                        buttons.Add(button);
                    }

                    if (move.Homing)
                    {
                        var button = new InlineKeyboardButton("Homing")
                        {
                            CallbackData = $"framedata:{move.Character.Name}:homing",
                        };
                        buttons.Add(button);
                    }

                    if (!string.IsNullOrWhiteSpace(move.StanceCode))
                    {
                        if (move.StanceName != null)
                        {
                            var button = new InlineKeyboardButton(move.StanceName)
                            {
                                CallbackData =
                                    $"framedata:{move.Character.Name}:stance:{move.StanceCode}",
                            };
                            buttons.Add(button);
                        }
                        else
                        {
                            throw new NullReferenceException(
                                "Tekken Stance Name is empty when Stance code is not"
                            );
                        }
                    }

                    if (move.Throw)
                    {
                        var button = new InlineKeyboardButton("Throw")
                        {
                            CallbackData = $"framedata:{move.Character.Name}:throw",
                        };
                        buttons.Add(button);
                    }

                    var markup = new InlineKeyboardMarkup(buttons);
                    return string.IsNullOrWhiteSpace(url)
                        ? await botClient.SendMessage(
                            message.Chat,
                            text,
                            ParseMode.Html,
                            replyMarkup: markup,
                            cancellationToken: cancellationToken
                        )
                        : await botClient.SendPhoto(
                            message.Chat,
                            InputFile.FromUri(url),
                            text,
                            showCaptionAboveMedia: true,
                            replyMarkup: markup,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken
                        );
                }
            }
            else
            {
                text = "Плохие параметры запроса фреймдаты.";
            }
        }

        return await botClient.SendMessage(
            message.Chat.Id,
            text,
            cancellationToken: cancellationToken
        );
    }
}
