using System.Collections.Generic;
using System.Linq;
using MARS.Server.Services.Twitch.Management.Entitys;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.Client.Models;

namespace MARS.Server.Exstensions;

public static class TwitchExstension
{
    public const string ChannelId = "785975641";
    public const string Channel = "rxdcodx";
    public const string BotName = "catisaai";
    public const string BotId = "888848441";
    public const string SevenTvUserId = "01G9FVE50G00022RD2T09E7QXC";

    public static ConcurrentBag<TwitchUser> BlackList = [];

    extension(ITwitchClient client)
    {
        public async Task SendMessageToMainTwitchAsync<T>(
            string message,
            ILogger<T>? logger = null
        )
            where T : class
        {
            try
            {
                if (
                    !client.JoinedChannels.Any(e =>
                        e.Channel.Equals(Channel, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    await client.JoinChannelAsync(Channel);
                }

                JoinedChannel? channel = client.GetJoinedChannel(Channel);
                if (channel != null)
                {
                    await client.SendMessageAsync(channel, message);
                }
            }
            catch (Exception e)
            {
                logger?.LogException(e);
            }
        }

        public async Task SendMessageToMainTwitchAsync(
            string message,
            ILogger? logger = null
        )
        {
            try
            {
                if (
                    !client.JoinedChannels.Any(e =>
                        e.Channel.Equals(Channel, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    await client.JoinChannelAsync(Channel);
                }

                JoinedChannel? channel = client.GetJoinedChannel(Channel);
                if (channel != null)
                {
                    await client.SendMessageAsync(channel, message);
                }
            }
            catch (Exception e)
            {
                logger?.LogException(e);
            }
        }
    }

    extension(ITwitchAPI client)
    {
        public async Task SendAnnouncementToMainTwitchAsync<T>(
            string message,
            TokenInfo? userToken,
            AnnouncementColors? color = null,
            ILogger<T>? logger = null
        )
            where T : class
        {
            color ??= AnnouncementColors.Primary;
            try
            {
                await client.Helix.Chat.SendChatAnnouncementAsync(
                    ChannelId,
                    ChannelId,
                    message,
                    color,
                    userToken?.AccessToken
                );
            }
            catch (Exception ex)
            {
                logger?.LogException(ex);
            }
        }

        public async Task<bool> ValidateToken<T>(
            ILogger<T> logger,
            string? token = null
        )
            where T : class
        {
            try
            {
                ValidateAccessTokenResponse? response = await client.Auth.ValidateAccessTokenAsync(
                    token ?? client.Settings.AccessToken
                );

                return response != null;
            }
            catch (Exception e)
                when (e.Message.Contains("invalid access token", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            catch (Exception e)
            {
                logger.LogException(e);
                return false;
            }
        }
    }

    extension<T>(IEnumerable<T> list) where T : TwitchUser
    {
        public HashSet<string> Logins => list.Select(e => e.UserLogin).ToHashSet();

        public HashSet<string> TwitchId => list.Select(e => e.TwitchId).ToHashSet();
    }
}
