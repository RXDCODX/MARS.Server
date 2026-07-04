using MARS.Server.Services.Twitch.TwitchFollowers;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Core.EventArgs.Channel;

namespace MARS.Server.Services.Twitch.Validation;

public sealed class TwitchEventValidationService(
    FollowerDbService followerDb
) : ITwitchEventValidationService
{
    public IMessageValidationBuilder ForMessageReceived(OnMessageReceivedArgs args)
    {
        return new MessageValidationBuilder(args, followerDb);
    }

    public IRedemptionValidationBuilder ForRedemption(
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        return new RedemptionValidationBuilder(args, followerDb);
    }
}
