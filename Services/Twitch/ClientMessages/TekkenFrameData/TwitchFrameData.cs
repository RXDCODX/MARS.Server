using System.Text.RegularExpressions;
using MARS.Server.Services.Framedata;
using MARS.Server.Services.Twitch.Rewards;

namespace MARS.Server.Services.Twitch.ClientMessages.TekkenFrameData;

public class TwitchFramedate(
    ILogger<TwitchFramedate> logger,
    ITwitchClient client,
    Tekken8FrameData frameData,
    IHostApplicationLifetime lifetime,
    IDbContextFactory<AppDbContext> factory
) : BackgroundService
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;
    private static readonly Regex Regex = new(@"\p{C}+");

    public async Task FrameDateMessage(object? sender, OnMessageReceivedArgs args)
    {
        if (
            args.ChatMessage.Channel.Equals(TwitchExstension.Channel)
            && !TwitchExstension.BlackList.Logins.Any(t =>
                t.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var channel = args.ChatMessage.Channel;

            await Task.Run(
                async () =>
                {
                    var message = args.ChatMessage.Message;

                    if (message.StartsWith("!fd ", StringComparison.OrdinalIgnoreCase))
                    {
                        var keyWords = Regex
                            .Replace(message, "")
                            .Split(
                                ' ',
                                StringSplitOptions.RemoveEmptyEntries
                                    | StringSplitOptions.TrimEntries
                            )
                            .Skip(1)
                            .Select(e => e.ToEnglishTransliteration().ToLower())
                            .ToArray();

                        if (keyWords.Length < 2)
                        {
                            await SendResponse(
                                channel,
                                "@{user}, плохие параметры запроса фреймдаты",
                                args.ChatMessage.DisplayName
                            );
                            return;
                        }

                        var response =
                            await HandleTagMoves(keyWords)
                            ?? await HandleStances(keyWords)
                            ?? await HandleSingleMove(keyWords);
                        if (response != null)
                        {
                            await SendResponse(channel, response, args.ChatMessage.DisplayName);
                        }
                        else
                        {
                            await SendResponse(
                                channel,
                                "@{user}, ничего не найдено по вашему запросу",
                                args.ChatMessage.DisplayName
                            );
                        }
                    }
                },
                _cancellationToken
            );
        }
    }

    private async Task<string?> HandleTagMoves(string[] keyWords)
    {
        var result = await frameData.GetMultipleMovesByTags(string.Join(' ', keyWords));
        if (result is not { Item2.Length: > 1 })
        {
            return null;
        }

        var character = await frameData.GetTekkenCharacter(string.Join(' ', keyWords.SkipLast(1)));
        return character == null
            ? null
            : $"\u2705 {character.Name} \u2705 {Enum.GetName(result.Value.Tag)} | "
                + $"Команды: {string.Join(", ", result.Value.Moves.Select(e => e.Command))}";
    }

    private async Task<string?> HandleStances(string[] keyWords)
    {
        if (!keyWords.Last().StartsWith("stance", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var charName = string.Join(' ', keyWords.SkipLast(1));
        var stances = await frameData.GetCharacterStances(charName, _cancellationToken);
        if (stances is not { Count: > 0 })
        {
            return null;
        }

        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
        var character = await frameData.FindCharacterInDatabaseAsync(charName, dbContext);
        return character == null
            ? null
            : $"\u2705 {character.Name} \u2705 Стойки: "
                + string.Join(", ", stances.Select(e => $"{e.Key} - {e.Value}"));
    }

    private async Task<string?> HandleSingleMove(string[] keyWords)
    {
        var move = await frameData.GetMoveAsync(keyWords);
        if (move?.Character == null)
        {
            return null;
        }

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

    private async Task SendResponse(string channel, string message, string username)
    {
        message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(username, message);

        try
        {
            if (
                !client.JoinedChannels.Any(e =>
                    e.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                await client.JoinChannelAsync(channel);
            }

            var joinedChannel = client.GetJoinedChannel(channel);

            // Twitch имеет ограничение на длину сообщения (500 символов)
            if (message.Length > 450)
            {
                message = message[..450] + "...";
            }

            if (joinedChannel != null)
            {
                await client.SendMessageAsync(joinedChannel, message);
            }
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Ошибка при отправке сообщения в Twitch");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += FrameDateMessage;
        });

        return Task.CompletedTask;
    }
}
