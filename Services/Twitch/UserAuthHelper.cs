using MARS.Server.Services.Twitch.FumoFriday;
using MARS.Server.Services.Twitch.Rewards.TwitchAlerts;
using MARS.Server.Services.Twitch.Rewards.TwitchRandomMeme;
using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;
using MARS.Server.Services.Twitch.StreamBotNotifications;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets;
using Timer = System.Timers.Timer;

namespace MARS.Server.Services.Twitch;

public class UserAuthHelper(
    ITwitchAPI api,
    ILogger<UserAuthHelper> logger,
    ITelegramBotClient client,
    IOptions<TelegramConfiguration> config,
    TwitchStreamStartupNotifications twitchStreamStartupNotifications,
    TwitchMediaAlerts twitchMediaAlerts,
    RollWaifu rollWaifu,
    IDbContextFactory<AppDbContext> factory,
    IServer server,
    RandomMeme twitchRandomMeme,
    FumoFridayWorker fumoFridayWorker
) : IHostedService
{
    public static readonly EventSubWebsocketClient WsClient = new();
    private readonly long[] _adminsArray = config.Value.AdminIdsArray;

    private Timer? _timer;
    private TokenInfo? _tokenInfo;

    private bool FirstActivation { get; set; } = true;

    public TokenInfo? Token
    {
        get => _tokenInfo;
        private set
        {
            if (value != null)
            {
                if (_tokenInfo?.Id != null)
                {
                    value.Id = _tokenInfo.Id;
                }

                _tokenInfo = value;
                UpdatePubSub(value.AccessToken).Wait();
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(TimeSpan.FromSeconds(30));
        _timer.AutoReset = true;

        _timer.Elapsed += async (_, __) =>
        {
            if (Token != null)
            {
                if (DateTimeOffset.Now >= Token.WhenExpires)
                {
                    var validated = await api.ValidateToken(logger, Token.AccessToken);

                    if (!validated)
                    {
                        var isRefreshed = await RefreshToken(Token);

                        if (!isRefreshed)
                        {
                            NotifStreamerAboutAuth();
                        }
                    }
                }
            }
        };

        _timer.Start();

        try
        {
            await using AppDbContext context = await factory.CreateDbContextAsync(
                cancellationToken
            );
            TokenInfo? tokenInfo = await context.TwitchToken.SingleOrDefaultAsync(
                cancellationToken
            );

            if (string.IsNullOrWhiteSpace(tokenInfo?.AccessToken))
            {
                NotifStreamerAboutAuth();
            }
            else
            {
                var validated = await api.ValidateToken(logger, tokenInfo.AccessToken);

                if (validated)
                {
                    Token = tokenInfo;
                }
                else
                {
                    await RefreshToken(tokenInfo);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer!.Stop();
        _timer.Dispose();
        return Task.CompletedTask;
    }

    private async Task UpdatePubSub(string? token = null)
    {
        token ??= Token?.AccessToken;

        if (!FirstActivation)
        {
            if (token != null)
            {
                GetEventSubSubscriptionsResponse? result = await GetEventSubs(token);

                if (result != null)
                {
                    if (
                        !result.Subscriptions.Any(e =>
                            e.Status.Equals("enabled", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        await WsClient.DisconnectAsync();
                        await WsClient.ConnectAsync();
                    }
                }
            }
        }

        if (FirstActivation)
        {
            WsClient.StreamOnline += twitchStreamStartupNotifications.PubSubOnlineOnStreamUp;

            WsClient.StreamOffline += twitchStreamStartupNotifications.PubSibOfflineStream;

            WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                twitchMediaAlerts.TwitchClientOnOnMessageSend;
            WsClient.ChannelPointsCustomRewardRedemptionAdd += rollWaifu.RollWaifuTwitchEvent;
            WsClient.ChannelPointsCustomRewardRedemptionAdd += twitchRandomMeme.RandomMemeHandler;
            WsClient.ChannelPointsCustomRewardRedemptionAdd += fumoFridayWorker.OnRewardRedemption;

            WsClient.WebsocketConnected += (_, _) => Reconnect(token);

            WsClient.ErrorOccurred += (_, args) =>
            {
                logger.LogException(args.Exception);
                return Task.CompletedTask;
            };

            WsClient.WebsocketReconnected += async (sender, args) =>
            {
                await Reconnect(token);
                await Task.Delay(1000);
            };

            WsClient.WebsocketDisconnected += async (sender, args) =>
            {
                while (!await WsClient.ReconnectAsync())
                {
                    await Task.Delay(30 * 1000);
                }
            };

            FirstActivation = false;

            await WsClient.ConnectAsync();
        }
    }

    public async Task Reconnect(string? token)
    {
        token = string.IsNullOrWhiteSpace(token) ? Token?.AccessToken : token;

        GetEventSubSubscriptionsResponse? subscriptions =
            await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                clientId: api.Settings.ClientId,
                accessToken: token
            );

        foreach (EventSubSubscription? subscription in subscriptions.Subscriptions)
        {
            await api.Helix.EventSub.DeleteEventSubSubscriptionAsync(
                subscription.Id,
                api.Settings.ClientId,
                token
            );
        }

        var condition = new Dictionary<string, string>
        {
            { "from_broadcaster_user_id", TwitchExstension.ChannelId },
        };

        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "channel.raid",
            "1",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );
        condition.Clear();
        condition.Add("broadcaster_user_id", TwitchExstension.ChannelId);

        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "stream.online",
            "1",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );
        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "stream.offline",
            "1",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );
        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "channel.channel_points_custom_reward_redemption.add",
            "1",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );

        condition.Add("moderator_user_id", TwitchExstension.ChannelId);

        await api.Helix.EventSub.CreateEventSubSubscriptionAsync(
            "channel.follow",
            "2",
            condition,
            EventSubTransportMethod.Websocket,
            WsClient.SessionId,
            null,
            null,
            api.Settings.ClientId,
            token
        );

        condition.Clear();

        GetEventSubSubscriptionsResponse? response =
            await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                clientId: api.Settings.ClientId,
                accessToken: token
            );
        if (response.Subscriptions.Length < 1)
        {
            logger.LogError("Не получилось подписать EventSub");
        }
        else
        {
            IEnumerable<string> aa = response.Subscriptions.Select(e => e.Type);
            var message = string.Join(Environment.NewLine, aa);
            await client.SendMessage(402763435, "Подключенные ивенты для твича: " + message);
        }
    }

    public async Task<GetEventSubSubscriptionsResponse?> GetEventSubs()
    {
        return await GetEventSubs(Token!.AccessToken);
    }

    public async Task<GetEventSubSubscriptionsResponse?> GetEventSubs(string acctoken)
    {
        try
        {
            GetEventSubSubscriptionsResponse? response =
                await api.Helix.EventSub.GetEventSubSubscriptionsAsync(
                    clientId: api.Settings.ClientId,
                    accessToken: acctoken
                );
            return response;
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }

        return null;
    }

    private async Task<bool> RefreshToken(TokenInfo refreshToken)
    {
        try
        {
            await using AppDbContext dbCOntext = await factory.CreateDbContextAsync();

            RefreshResponse? result = await api.Auth.RefreshAuthTokenAsync(
                refreshToken.RefreshToken,
                api.Settings.Secret,
                api.Settings.ClientId
            );
            refreshToken.AccessToken = result.AccessToken;
            refreshToken.ExpiresIn = TimeSpan.FromSeconds(result.ExpiresIn);
            refreshToken.RefreshToken = result.RefreshToken;
            refreshToken.WhenCreated = DateTimeOffset.Now.AddSeconds(-30);

            Token = refreshToken;

            await dbCOntext.SaveChangesAsync();

            return true;
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }

        return false;
    }

    private async void NotifStreamerAboutAuth()
    {
        try
        {
            IServerAddressesFeature? addressesFeature =
                server.Features.Get<IServerAddressesFeature>();

            while (!addressesFeature!.Addresses.Any())
            {
                await Task.Delay(1000);
            }

            var address = addressesFeature.Addresses.FirstOrDefault();

            foreach (var t in _adminsArray)
            {
                await client.SendMessage(
                    _adminsArray[0],
                    "Нужно пройти переаунтефикацию для твича!"
                        + Environment.NewLine
                        + Environment.NewLine
                        + $"""https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={api.Settings.ClientId}&redirect_uri={address}/twitchuserauth&scope=analytics:read:extensions+user:edit+user:read:email+clips:edit+bits:read+analytics:read:games+user:edit:broadcast+user:read:broadcast+chat:read+chat:edit+channel:moderate+channel:read:subscriptions+whispers:read+whispers:edit+moderation:read+channel:read:redemptions+channel:edit:commercial+channel:read:hype_train+channel:read:stream_key+channel:manage:extensions+channel:manage:broadcast+user:edit:follows+channel:manage:redemptions+channel:read:editors+channel:manage:videos+user:read:blocked_users+user:manage:blocked_users+user:read:subscriptions+user:read:follows+channel:manage:polls+channel:manage:predictions+channel:read:polls+channel:read:predictions+moderator:manage:automod+channel:manage:schedule+channel:read:goals+moderator:read:automod_settings+moderator:manage:automod_settings+moderator:manage:banned_users+moderator:read:blocked_terms+moderator:manage:blocked_terms+moderator:read:chat_settings+moderator:manage:chat_settings+channel:manage:raids+moderator:manage:announcements+moderator:manage:chat_messages+user:manage:chat_color+channel:manage:moderators+channel:read:vips+channel:manage:vips+user:manage:whispers+channel:read:charity+moderator:read:chatters+moderator:read:shield_mode+moderator:manage:shield_mode+moderator:read:shoutouts+moderator:manage:shoutouts+moderator:read:followers+channel:read:guest_star+channel:manage:guest_star+moderator:read:guest_star+moderator:manage:guest_star+channel:bot+user:bot+user:read:chat+channel:manage:ads+channel:read:ads+user:read:moderated_channels+user:write:chat+user:read:emotes+moderator:read:unban_requests+moderator:manage:unban_requests+moderator:read:suspicious_users"""
                );
            }
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }
    }

    public async void ApplyNewTokenFromAuth(string accesstoken, string refreshToken, int expiresin)
    {
        await using AppDbContext dbcontext = await factory.CreateDbContextAsync();

        if (await dbcontext.TwitchToken.AnyAsync())
        {
            TokenInfo token = await dbcontext.TwitchToken.SingleAsync();

            token.AccessToken = accesstoken;
            token.RefreshToken = refreshToken;
            token.ExpiresIn = TimeSpan.FromSeconds(expiresin);
            token.WhenCreated = DateTimeOffset.Now.AddSeconds(-30);

            Token = token;
        }
        else
        {
            var tokenInfo = new TokenInfo
            {
                AccessToken = accesstoken,
                RefreshToken = refreshToken,
                ExpiresIn = TimeSpan.FromSeconds(expiresin),
                WhenCreated = DateTimeOffset.Now.AddSeconds(-30),
            };

            await dbcontext.AddAsync(tokenInfo);

            Token = tokenInfo;
        }

        await dbcontext.SaveChangesAsync();
    }
}
