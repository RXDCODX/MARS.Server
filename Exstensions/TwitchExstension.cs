using TwitchLib.Api.Auth;
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
    ];

    public static Task SendMessageToPyrokxnezxzAsync<T>(
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

            if (response == null)
            {
                return false;
            }

            return true;
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
