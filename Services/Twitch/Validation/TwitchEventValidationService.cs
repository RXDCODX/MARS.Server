using System.Collections.Concurrent;
using MARS.Server.Services.Twitch.TwitchFollowers;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;

namespace MARS.Server.Services.Twitch.Validation;

public sealed class TwitchEventValidationService(
    FollowerDbService followerDb,
    ITwitchClient client,
    ILogger<TwitchEventValidationService> logger,
    TwitchUserEnsureService userEnsureService
) : ITwitchEventValidationService
{
    internal readonly ConcurrentDictionary<string, DateTime> SentEventErrors = new();

    public IMessageValidationBuilder ForMessageReceived(OnMessageReceivedArgs args)
    {
        return new MessageValidationBuilder(
            args,
            followerDb,
            client,
            logger,
            userEnsureService,
            SentEventErrors
        );
    }

    public IRedemptionValidationBuilder ForRedemption(ChannelPointsCustomRewardRedemptionArgs args)
    {
        return new RedemptionValidationBuilder(
            args,
            followerDb,
            client,
            logger,
            userEnsureService,
            SentEventErrors
        );
    }
}
