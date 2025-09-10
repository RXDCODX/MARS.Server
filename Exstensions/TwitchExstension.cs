using MARS.Server.Services.Twitch.Management.Entitys;
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

    public static readonly List<string> BlackList =
    [
        BotName,
        "streamelements",
        "4vacking",
        "aspirantd",
        "nightbot",
        "moobot",
        "jeetbot",
    ];

    public static Task SendMessageToMainTwitchAsync<T>(
        this ITwitchClient client,
        string message,
        ILogger<T>? logger = default
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
                client.JoinChannel(Channel);
            }

            JoinedChannel? channel = client.GetJoinedChannel(Channel);
            client.SendMessage(channel, message);
        }
        catch (Exception e)
        {
            logger?.LogException(e);
        }

        return Task.CompletedTask;
    }

    public static Task SendMessageToMainTwitchAsync(
        this ITwitchClient client,
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
                client.JoinChannel(Channel);
            }

            JoinedChannel? channel = client.GetJoinedChannel(Channel);
            client.SendMessage(channel, message);
        }
        catch (Exception e)
        {
            logger?.LogException(e);
        }

        return Task.CompletedTask;
    }

    public static async Task SendAnnouncementToMainTwitch<T>(
        this ITwitchAPI client,
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

    public static async Task<bool> ValidateToken<T>(
        this ITwitchAPI api,
        ILogger<T> logger,
        string? token = null
    )
        where T : class
    {
        try
        {
            ValidateAccessTokenResponse? response = await api.Auth.ValidateAccessTokenAsync(
                token ?? api.Settings.AccessToken
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
