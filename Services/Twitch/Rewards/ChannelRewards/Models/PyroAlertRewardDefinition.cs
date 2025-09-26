namespace MARS.Server.Services.Twitch.Rewards.ChannelRewards.Models;

/// <summary>
/// Награда-обертка для PyroAlerts (связывается с MediaInfo через TwitchGuid)
/// </summary>
public sealed class PyroAlertRewardDefinition : ChannelRewardDefinition
{
    /// <summary>
    /// Id MediaInfo для привязки или null, если будем матчить по Cost
    /// </summary>
    public Guid? MediaInfoId { get; init; }
}


