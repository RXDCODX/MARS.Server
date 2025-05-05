using MARS.Server.Services.Framedata;
using MARS.Server.Services.Twitch.Rewards;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.ClientMessages.TekkenFrameData;

public class TwitchFramedate(
    ILogger<TwitchFramedate> logger,
    ITwitchClient client,
    Tekken8FrameData frameData,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    public async void FrameDateMessage(object? sender, OnMessageReceivedArgs args)
    {
        await Task.Run(async () =>
        {
            if (args.ChatMessage.Channel.Equals(TwitchExstension.Channel))
            {
                var message = args.ChatMessage.Message;

                if (message.StartsWith("!fd ", StringComparison.OrdinalIgnoreCase))
                {
                    var split = message.Split(' ');

                    if (split.Length > 2)
                    {
                        var charNameString = split.Skip(1).ToArray();
                        var move = await frameData
                            .GetMoveAsync(charNameString)
                            .ConfigureAwait(false);
                        var channel = args.ChatMessage.Channel;

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
            }
        });
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
