using TwitchLib.Client.Events;
using TwitchLib.EventSub.Core.EventArgs.Channel;

namespace MARS.Server.Services.Twitch.Validation;

public interface ITwitchEventValidationService
{
    IMessageValidationBuilder ForMessageReceived(OnMessageReceivedArgs args);
    IRedemptionValidationBuilder ForRedemption(ChannelPointsCustomRewardRedemptionArgs args);
}
