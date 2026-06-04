using MARS.Server.Services.Framedata;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class FramedataCommand(Tekken8FrameData frameData, IDbContextFactory<AppDbContext> factory)
    : BaseCommand
{
    public override string CommandName => "framedata";
    public override string Description => "Показывает фреймдату по персонажам Tekken 8 и их ударам";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override string[] Aliases => ["fd", "frame", "frames"];

    public override CommandVisibility Visibility => CommandVisibility.All; // Видна везде

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "character",
                Description = "Имя персонажа",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "move",
                Description = "Команда удара или тег",
                Type = "string",
                Required = true,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !parameters.TryGetValue("character", out var characterObj)
            || !parameters.TryGetValue("move", out var moveObj)
        )
        {
            return GetErrorMessage(platform);
        }

        var character = characterObj.ToString() ?? "";
        var move = moveObj.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(move))
        {
            return GetErrorMessage(platform);
        }

        var keyWords = $"{character} {move}"
            .Split(' ')
            .Select(e => e.ToEnglishTransliteration().ToLower())
            .ToArray();

        var tagMovesResult = await HandleTagMoves(keyWords, platform);
        if (!string.IsNullOrEmpty(tagMovesResult))
        {
            return tagMovesResult;
        }

        var stancesResult = await HandleStances(keyWords, platform, cancellationToken);
        if (!string.IsNullOrEmpty(stancesResult))
        {
            return stancesResult;
        }

        var singleMoveResult = await HandleSingleMove(keyWords, platform);
        return !string.IsNullOrEmpty(singleMoveResult)
            ? singleMoveResult
            : GetErrorMessage(platform);
    }

    private static string GetErrorMessage(Platform platform)
    {
        return platform switch
        {
            Platform.Twitch => "Плохие параметры запроса фреймдаты.",
            _ =>
                "Необходимо указать персонажа и команду удара. Пример: /framedata jin kazama 1,2,3",
        };
    }

    private async Task<string> HandleTagMoves(string[] keyWords, Platform platform)
    {
        var result = await frameData.GetMultipleMovesByTags(string.Join(' ', keyWords));
        if (result is not { Item2.Length: > 1 })
        {
            return string.Empty;
        }

        var character = await frameData.GetTekkenCharacter(string.Join(' ', keyWords.SkipLast(1)));
        return character == null
            ? string.Empty
            : platform switch
            {
                Platform.Twitch =>
                    $"\u2705 {character.Name} \u2705 {Enum.GetName(result.Value.Tag)} | Команды: {string.Join(", ", result.Value.Moves.Select(e => e.Command))}",
                _ =>
                    $"""
                    🎭 Character 🎭
                    {character.Name}

                    ///////////////////////////////
                    {Enum.GetName(result.Value.Tag)}

                    {string.Join(
                        Environment.NewLine,
                        result.Value.Moves.Select(e => $"{e.Command}")
                    )}
                    """,
            };
    }

    private async Task<string> HandleStances(
        string[] keyWords,
        Platform platform,
        CancellationToken cancellationToken
    )
    {
        if (!keyWords.Last().StartsWith("stance", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var charName = string.Join(' ', keyWords.SkipLast(1));
        var stances = await frameData.GetCharacterStances(charName, cancellationToken);
        if (stances is not { Count: > 0 })
        {
            return string.Empty;
        }

        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        var character = await frameData.FindCharacterInDatabaseAsync(charName, dbContext);
        return character == null
            ? string.Empty
            : platform switch
            {
                Platform.Twitch =>
                    $"\u2705 {character.Name} \u2705 Стойки: {string.Join(", ", stances.Select(e => $"{e.Key} - {e.Value}"))}",
                _ =>
                    $"""
            🎭 Character 🎭
            {character.Name}

            ///////////////////////////////
            Stance code - Stance Name

            {string.Join(
                Environment.NewLine,
                stances.Select(e => $"{e.Key} - {e.Value}")
            )}
            """,
            };
    }

    private async Task<string> HandleSingleMove(string[] keyWords, Platform platform)
    {
        var move = await frameData.GetMoveAsync(keyWords);
        return move?.Character == null
            ? string.Empty
            : platform switch
            {
                Platform.Twitch => FormatTwitchMove(move),
                _ => $"""
                    🎭 Character 🎭
                    {move.Character.Name}

                    ///////////////////
                    🔡 Input 🔡
                    {move.Command}

                    🚀 Startup 🚀
                    {move.StartUpFrame}

                    🏁 Block frame 🏁
                    {move.BlockFrame}

                    🎯 Hit frame 🎯
                    {move.HitFrame}

                    🤝 Counter hit frame 🤝
                    {move.CounterHitFrame}

                    ///////////////////
                    📊 Hit Level 📊
                    {move.HitLevel}

                    💥 Damage 💥
                    {move.Damage}

                    {(move.Notes is { Length: > 0 } ? "\ud83d\udcdd Notes \ud83d\udcdd" : null)} 
                    {string.Join(Environment.NewLine, move.Notes ?? [])}
                    """,
            };
    }

    private static string FormatTwitchMove(dynamic move)
    {
        var tags = new List<string>();
        if (move.HeatEngage)
        {
            tags.Add("Heat Engager");
        }

        if (move.Tornado)
        {
            tags.Add("Tornado");
        }

        if (move.HeatSmash)
        {
            tags.Add("Heat Smash");
        }

        if (move.PowerCrush)
        {
            tags.Add("Power Crush");
        }

        if (move.HeatBurst)
        {
            tags.Add("Heat Burst");
        }

        if (move.Homing)
        {
            tags.Add("Homing");
        }

        if (move.Throw)
        {
            tags.Add("Throw");
        }

        var stanceInfo = !string.IsNullOrWhiteSpace(move.StanceCode)
            ? $" | Стойка: {move.StanceName} ({move.StanceCode})"
            : "";

        var tagsInfo = tags.Count > 0 ? $" | Теги: {string.Join(", ", tags)}" : "";

        return $"\u2705 {move.Character.Name} > {move.Command} \u2705 "
            + $"Старт: {move.StartUpFrame} | Блок: {move.BlockFrame} | Хит: {move.HitFrame} | "
            + $"CH: {move.CounterHitFrame} | Уровень: {move.HitLevel} | Урон: {move.Damage}"
            + stanceInfo
            + tagsInfo;
    }
}
