using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Message = Telegram.Bot.Types.Message;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [Alias("fd")]
    public async Task<Message> OnFramedataCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var split = message.Text?.Split(" ");
        if (split is not { Length: >= 3 })
        {
            return await SendDefaultResponse();
        }

        var keyWords = split.Skip(1).Select(e => e.ToEnglishTransliteration().ToLower()).ToArray();
        var response = await HandleTagMoves() ?? await HandleStances() ?? await HandleSingleMove();
        return response ?? await SendDefaultResponse();

        async Task<Message?> HandleTagMoves()
        {
            var result = await frameData.GetMultipleMovesByTags(string.Join(' ', keyWords));
            if (result is not { Item2.Length: > 1 })
            {
                return null;
            }

            var character = await frameData.GetTekkenCharacter(
                string.Join(' ', keyWords.SkipLast(1))
            );
            if (character == null)
            {
                return null;
            }

            var text = $"""
                🎭 <b>Character</b> 🎭
                <i>{character.Name}</i>

                ///////////////////////////////
                {Enum.GetName(result.Value.Tag)}

                {string.Join(Environment.NewLine, result.Value.Moves.Select(e => $"{e.Command}"))}
                """;

            return await SendResponse(text, null, character?.LinkToImage);
        }

        async Task<Message?> HandleStances()
        {
            if (!keyWords.Last().StartsWith("stance", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var charName = string.Join(' ', keyWords.SkipLast(1));
            var stances = await frameData.GetCharacterStances(charName, cancellationToken);
            if (stances is not { Count: > 0 })
            {
                return null;
            }

            await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
            var character = await frameData.FindCharacterInDatabaseAsync(charName, dbContext);
            if (character == null)
            {
                return null;
            }

            var text = $"""
                        🎭 <b>Character</b> 🎭
                        <i>{character.Name}</i>

                        ///////////////////////////////
                        Stance code - Stance Name

                        {string.Join(
                            Environment.NewLine,
                            stances.Select(e => $"<i>{e.Key}</i> - <i>{e.Value}</i>")
                        )}
                        """;

            var buttons = stances
                .Select(s =>
                    new[]
                    {
                        new InlineKeyboardButton(s.Value)
                        {
                            CallbackData = $"framedata:{character.Name}:stance:{s.Key}",
                        },
                    }
                )
                .ToArray();

            return await SendResponse(
                text,
                new InlineKeyboardMarkup(buttons),
                character.LinkToImage
            );
        }

        async Task<Message?> HandleSingleMove()
        {
            var move = await frameData.GetMoveAsync(keyWords);
            if (move?.Character == null)
            {
                return null;
            }

            var text = $"""
                🎭 <b>Character</b> 🎭
                <i>{move.Character.Name}</i>

                ///////////////////
                🔡 <b>Input</b> 🔡
                <i>{move.Command}</i>

                🚀 <b>Startup</b> 🚀
                <i>{move.StartUpFrame}</i>

                🏁 <b>Block frame</b> �
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
            AddButtonIf(move.HeatEngage, "Heat Engager", "heatengage");
            AddButtonIf(move.Tornado, "Tornado", "tornado");
            AddButtonIf(move.HeatSmash, "Heat Smash", "heatsmash");
            AddButtonIf(move.PowerCrush, "Power Crush", "powercrush");
            AddButtonIf(move.HeatBurst, "Heat Burst", "heatburst");
            AddButtonIf(move.Homing, "Homing", "homing");
            AddButtonIf(move.Throw, "Throw", "throw");

            if (!string.IsNullOrWhiteSpace(move.StanceCode))
            {
                buttons.Add(
                    new InlineKeyboardButton(move.StanceName!)
                    {
                        CallbackData = $"framedata:{move.Character.Name}:stance:{move.StanceCode}",
                    }
                );
            }

            return await SendResponse(
                text,
                new InlineKeyboardMarkup(buttons),
                move.Character.LinkToImage
            );

            void AddButtonIf(bool condition, string textForButton, string callbackSuffix)
            {
                if (condition)
                {
                    buttons.Add(
                        new InlineKeyboardButton(textForButton)
                        {
                            CallbackData = $"framedata:{move.Character.Name}:{callbackSuffix}",
                        }
                    );
                }
            }
        }

        async Task<Message> SendResponse(
            string text,
            InlineKeyboardMarkup? markup,
            string? imageUrl
        )
        {
            return string.IsNullOrWhiteSpace(imageUrl)
                ? await botClient.SendMessage(
                    message.Chat,
                    text,
                    ParseMode.Html,
                    replyMarkup: markup,
                    cancellationToken: cancellationToken
                )
                : await botClient.SendPhoto(
                    message.Chat,
                    InputFile.FromUri(imageUrl),
                    text,
                    showCaptionAboveMedia: true,
                    replyMarkup: markup,
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken
                );
        }

        async Task<Message> SendDefaultResponse()
        {
            return await botClient.SendMessage(
                message.Chat.Id,
                "Плохие параметры запроса фреймдаты.",
                cancellationToken: cancellationToken
            );
        }
    }
}
