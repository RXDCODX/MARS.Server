namespace MARS.Server.Services.Twitch.Management;

public class TwitchAuthService(
    ITwitchAPI api,
    ILogger<TwitchAuthService> logger,
    TokenService tokenService,
    EventSubService eventSubService,
    TelegramTokenNotification telegramNotificationService
) : IHostedService
{
    private Timer? _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(TimeSpan.FromSeconds(30));
        _timer.AutoReset = true;

        _timer.Elapsed += async (_, __) =>
        {
            if (tokenService.Token != null)
            {
                if (DateTimeOffset.Now >= tokenService.Token.WhenExpires)
                {
                    var validated = await api.ValidateToken(logger, tokenService.Token.AccessToken);

                    if (!validated)
                    {
                        var isRefreshed = await tokenService.RefreshTokenAsync(tokenService.Token);

                        if (!isRefreshed)
                        {
                            await telegramNotificationService.NotifyStreamerAboutAuthAsync(api);
                        }
                        else
                        {
                            // Обновляем подписки EventSub после успешного обновления токена
                            await eventSubService.UpdatePubSubAsync(tokenService.Token.AccessToken);
                        }
                    }
                }
            }
        };

        _timer.Start();

        try
        {
            var tokenInfo = await tokenService.GetTokenAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(tokenInfo?.AccessToken))
            {
                await telegramNotificationService.NotifyStreamerAboutAuthAsync(api);
            }
            else
            {
                var validated = await api.ValidateToken(logger, tokenInfo.AccessToken);

                if (validated)
                {
                    tokenService.Token = tokenInfo;
                    // Инициализируем подписки EventSub при старте
                    await eventSubService.UpdatePubSubAsync(tokenInfo.AccessToken);
                }
                else
                {
                    var isRefreshed = await tokenService.RefreshTokenAsync(tokenInfo);
                    if (isRefreshed)
                    {
                        // Обновляем подписки EventSub после успешного обновления токена
                        await eventSubService.UpdatePubSubAsync(tokenInfo.AccessToken);
                    }
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
}
