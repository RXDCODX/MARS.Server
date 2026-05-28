using System.Collections.Generic;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;

namespace MARS.Server.Services.Twitch.TwitchFollowers.Entitys;

public class ChannelUsersResult
{
    public required List<ChannelFollower> Followers { get; set; }
    public required List<ChannelVIPsResponseModel> ViPs { get; set; }
    public required List<Moderator> Moderators { get; set; }
}
