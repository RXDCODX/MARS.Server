using System.Text.RegularExpressions;
using MARS.Server.Services.Framedata;
using MARS.Server.Services.Twitch.Rewards;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TwitchLib.Client.Events;

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
    private static readonly Regex Regex = new Regex(@"\p{C}+");

    public async void FrameDateMessage(object? sender, OnMessageReceivedArgs args)
    {
        if (args.ChatMessage.Channel.Equals(TwitchExstension.Channel))
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
                            );

                        if (keyWords.Length > 2)
                        {
                            var charNameString = keyWords.Skip(1).ToArray();

                            if (
                                charNameString
                                    .Last()
                                    .StartsWith("stance", StringComparison.OrdinalIgnoreCase)
                            )
                            {
                                var charName = string.Join(' ', charNameString.SkipLast(1));

                                var stances = await frameData.GetCharacterStances(
                                    charName,
                                    _cancellationToken
                                );

                                if (stances is { Count: > 0 })
                                {
                                    await using var dbContext = await factory.CreateDbContextAsync(
                                        _cancellationToken
                                    );
                                    var character = await frameData.FindCharacterInDatabaseAsync(
                                        charName,
                                        dbContext
                                    );
                                    var url = character?.LinkToImage;

                                    message =
                                        "✅ "
                                        + character?.Name
                                        + " ✅ "
                                        + string.Join(
                                            '|',
                                            stances.Select(e => $" {e.Key} - {e.Value} ")
                                        );

                                    try
                                    {
                                        if (
                                            !client.JoinedChannels.Any(e =>
                                                e.Channel.Equals(
                                                    channel,
                                                    StringComparison.OrdinalIgnoreCase
                                                )
                                            )
                                        )
                                        {
                                            client.JoinChannel(channel);
                                        }

                                        var joinedChannel = client.GetJoinedChannel(channel);
                                        client.SendMessage(joinedChannel, message);
                                        return;
                                    }
                                    catch (Exception e)
                                    {
                                        logger?.LogError(e.Message, e.StackTrace);
                                    }
                                }
                            }

                            var move = await frameData
                                .GetMoveAsync(charNameString)
                                .ConfigureAwait(false);

                            if (move != null)
                            {
                                var teges = await frameData.GetMoveTags(move);

                                message =
                                    "✅ "
                                    + move.Character!.Name
                                    + " > "
                                    + move.Command
                                    + " ✅  "
                                    + "Block: "
                                    + move.BlockFrame
                                    + " | Dmg: "
                                    + move.Damage
                                    + " | Hit: "
                                    + move.HitFrame
                                    + " | HitLvl: "
                                    + move.HitLevel
                                    + " | StartUp: "
                                    + move.StartUpFrame
                                    + (string.IsNullOrEmpty(teges) ? "" : " | Tags: " + teges);

                                try
                                {
                                    if (
                                        !client.JoinedChannels.Any(e =>
                                            e.Channel.Equals(
                                                channel,
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                        )
                                    )
                                    {
                                        client.JoinChannel(channel);
                                    }

                                    var joinedChannel = client.GetJoinedChannel(channel);
                                    client.SendMessage(joinedChannel, message);
                                    return;
                                }
                                catch (Exception e)
                                {
                                    logger?.LogError(e.Message, e.StackTrace);
                                }
                            }

                            const string tempLate = @"@{user}, кривые параметры запроса фреймдаты";

                            message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                args.ChatMessage.DisplayName,
                                tempLate
                            );

                            if (
                                !client.JoinedChannels.Any(e =>
                                    e.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)
                                )
                            )
                            {
                                client.JoinChannel(channel);
                            }

                            var joined = client.GetJoinedChannel(channel);
                            client.SendMessage(joined, message);
                        }
                    }
                },
                _cancellationToken
            );
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
