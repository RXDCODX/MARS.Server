using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;

namespace MARS.Server.Services.Twitch.TwitchFollowers;

public class TwitchViewersService(ITwitchAPI api, TokenService tokenService)
{
    public async Task<List<ChannelFollower>?> GetAllFollowers()
    {
        if (tokenService.Token != null)
        {
            var pagination = "1";
            var list = new List<ChannelFollower>();

            while (!string.IsNullOrWhiteSpace(pagination))
            {
                pagination = string.Empty;
                var result = await api.Helix.Channels.GetChannelFollowersAsync(
                    TwitchExstension.ChannelId,
                    null,
                    100,
                    pagination,
                    tokenService.Token.AccessToken
                );
                pagination = result.Pagination?.Cursor ?? string.Empty;
                list.AddRange(result.Data);
            }

            return list;
        }

        return null;
    }

    public async Task<List<ChannelVIPsResponseModel>?> GetAllViPs()
    {
        if (tokenService.Token != null)
        {
            var pagination = "1";
            var list = new List<ChannelVIPsResponseModel>();

            while (!string.IsNullOrWhiteSpace(pagination))
            {
                pagination = string.Empty;
                var result = await api.Helix.Channels.GetVIPsAsync(
                    TwitchExstension.ChannelId,
                    null,
                    100,
                    pagination,
                    tokenService.Token!.AccessToken
                );
                pagination = result.Pagination?.Cursor ?? string.Empty;
                list.AddRange(result.Data);
            }

            return list;
        }

        return null;
    }

    public async Task<List<Moderator>?> GetModerators()
    {
        if (tokenService.Token != null)
        {
            var pagination = "1";
            var list = new List<Moderator>();

            while (!string.IsNullOrWhiteSpace(pagination))
            {
                pagination = string.Empty;
                var result = await api.Helix.Moderation.GetModeratorsAsync(
                    TwitchExstension.ChannelId,
                    null,
                    100,
                    pagination,
                    tokenService.Token!.AccessToken
                );
                pagination = result.Pagination?.Cursor ?? string.Empty;
                list.AddRange(result.Data);
            }

            return list;
        }

        return null;
    }
}
