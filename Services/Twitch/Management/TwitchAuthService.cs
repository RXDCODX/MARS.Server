using MARS.Server.Services.Twitch.Management.Entitys;

namespace MARS.Server.Services.Twitch.Management;

public class TwitchAuthService(
    ITwitchAPI api,
    ILogger<TwitchAuthService> logger,
    TokenService tokenService,
    EventSubService eventSubService,
    TelegramTokenNotification telegramNotificationService
) : BackgroundService
{
    private const int CheckIntervalSeconds = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Initial token check
            var tokenInfo = await tokenService.GetTokenAsync(stoppingToken);

            if (string.IsNullOrWhiteSpace(tokenInfo?.AccessToken))
            {
                await telegramNotificationService.NotifyStreamerAboutAuthAsync(api);
            }
            else if (!await ValidateAndRefreshToken(tokenInfo))
            {
                await telegramNotificationService.NotifyStreamerAboutAuthAsync(api);
            }
            else
            {
                tokenService.Token = tokenInfo;
            }

            // Main loop
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(CheckIntervalSeconds), stoppingToken);

                if (tokenService.Token != null)
                {
                    await ValidateAndRefreshToken(tokenService.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Service is stopping
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }
    }

    private async Task<bool> ValidateAndRefreshToken(TokenInfo token)
    {
        if (DateTimeOffset.Now < token.WhenExpires)
        {
            await eventSubService.UpdateEventSubbAsync(token);
            return true;
        }

        var validated = await api.ValidateToken(logger, token.AccessToken);
        if (validated)
        {
            await eventSubService.UpdateEventSubbAsync(token);
            return true;
        }

        var isRefreshed = await tokenService.RefreshTokenAsync(token);
        if (isRefreshed)
        {
            await eventSubService.UpdateEventSubbAsync(token);
            return true;
        }

        return false;
    }
}
