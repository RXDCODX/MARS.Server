using System.Collections.Concurrent;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchAlerts;

public class TwitchMediaAlerts(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> dbContextFactory
)
{
    private readonly ConcurrentDictionary<string, OnMessageReceivedArgs> _normalMessages = new();
    private readonly ConcurrentDictionary<
        string,
        ChannelPointsCustomRewardRedemptionArgs?
    > _pointsMessages = new();

    private async Task GetAndSendAlert(string message)
    {
        ChannelPointsCustomRewardRedemptionArgs? argsCustomRewardArgs;

        while (!_pointsMessages.TryGetValue(message, out argsCustomRewardArgs))
        {
            await Task.Delay(50);
        }

        var isAlertWithUserInput = !string.IsNullOrWhiteSpace(
            argsCustomRewardArgs?.Notification.Payload.Event.UserInput
        );

        if (
            isAlertWithUserInput
                ? _normalMessages.ContainsKey(message) && _pointsMessages.ContainsKey(message)
                : _pointsMessages.ContainsKey(message) && !_normalMessages.ContainsKey(message)
        )
        {
            while (
                !_pointsMessages.TryRemove(message, out ChannelPointsCustomRewardRedemptionArgs? b)
            )
            {
                await Task.Delay(50);

                if (!_pointsMessages.ContainsKey(message))
                {
                    break;
                }
            }

            if (isAlertWithUserInput)
            {
                while (!_normalMessages.TryGetValue(message, out OnMessageReceivedArgs? _))
                {
                    await Task.Delay(50);

                    if (!_normalMessages.ContainsKey(message))
                    {
                        break;
                    }
                }

                while (!_normalMessages.TryRemove(message, out OnMessageReceivedArgs? b))
                {
                    await Task.Delay(50);

                    if (!_normalMessages.ContainsKey(message))
                    {
                        break;
                    }
                }
            }

            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            var mediaList = dbContext
                .Alerts.AsNoTracking()
                .AsEnumerable()
                .Where(e =>
                    e.MetaInfo.TwitchPointsCost
                    == argsCustomRewardArgs!.Notification.Payload.Event.Reward.Cost
                )
                .ToList();

            MediaInfo? mediaOld = null;

            if (mediaList.Count == 1)
            {
                mediaOld = mediaList[0];
            }
            else if (mediaList.Count > 1)
            {
                var index = Random.Shared.Next(mediaList.Count);
                mediaOld = mediaList[index];
            }

            if (mediaOld != null)
            {
                var mediaClone = (MediaInfo)mediaOld.CloneTo();
                mediaClone.FixAlertText(
                    argsCustomRewardArgs!.Notification.Payload.Event.UserName,
                    argsCustomRewardArgs.Notification.Payload.Event.UserInput
                );

                await hubContext.Clients.All.Alert(new MediaDto { MediaInfo = mediaClone });
            }
        }
    }

    internal async void TwitchClientOnNormalMessage(object? sender, OnMessageReceivedArgs args)
    {
        await Task.Run(async () =>
        {
            if (
                args.ChatMessage.Channel.Equals(
                    TwitchExstension.Channel,
                    StringComparison.OrdinalIgnoreCase
                )
                && !TwitchExstension.BlackList.Any(u =>
                    u.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                var value = args.ChatMessage.Message.ToLower().Trim();

                while (!_normalMessages.TryAdd(value, args))
                {
                    await Task.Delay(50);
                }

                await GetAndSendAlert(value);
            }
        });
    }

    internal async Task TwitchClientOnOnMessageSend(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        if (
            args.Notification.Payload.Event.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var value = args.Notification.Payload.Event.UserInput.ToLower().Trim();

            while (!_pointsMessages.TryAdd(value, args))
            {
                await Task.Delay(50);
            }

            await GetAndSendAlert(value);
        }
    }
}
