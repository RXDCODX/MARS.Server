using MARS.Server.Services.Twitch.TwitchFollowers;
using Microsoft.Extensions.Logging;
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
    public IMessageValidationBuilder ForMessageReceived(OnMessageReceivedArgs args)
    {
        return new MessageValidationBuilder(args, followerDb, client, logger, userEnsureService);
    }

    public IRedemptionValidationBuilder ForRedemption(ChannelPointsCustomRewardRedemptionArgs args)
    {
        return new RedemptionValidationBuilder(args, followerDb, client, logger, userEnsureService);
    }
}
