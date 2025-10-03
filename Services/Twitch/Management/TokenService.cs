using MARS.Server.Services.Twitch.Management.Entitys;
using TwitchLib.Api;

namespace MARS.Server.Services.Twitch.Management;

public class TokenService(
    ITwitchAPI api,
    ILogger<TokenService> logger,
    IDbContextFactory<AppDbContext> factory,
    TelegramTokenNotification notification
)
{
    private static readonly SemaphoreSlim SemaphoreSlim = new(1, 1);
    private TokenInfo? _tokenInfo;

    public TokenInfo? Token
    {
        get => _tokenInfo ??= GetFirstTokenAsync().GetAwaiter().GetResult();
        internal set
        {
            if (value != null)
            {
                if (_tokenInfo?.Id != null)
                {
                    value.Id = _tokenInfo.Id;
                }

                _tokenInfo = value;
            }
        }
    }

    public async Task<TokenInfo?> GetTokenAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.TwitchToken.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> RefreshTokenAsync(TokenInfo refreshToken)
    {
        try
        {
            await using AppDbContext dbContext = await factory.CreateDbContextAsync();

            var fakeTwitchApi = new TwitchAPI();
            var result = await fakeTwitchApi.Auth.RefreshAuthTokenAsync(
                refreshToken.RefreshToken,
                api.Settings.Secret,
                api.Settings.ClientId
            );

            var token = await dbContext.TwitchToken.AsNoTracking().SingleAsync();

            token.AccessToken = result.AccessToken;
            token.ExpiresIn = TimeSpan.FromSeconds(result.ExpiresIn);
            token.RefreshToken = result.RefreshToken;
            token.WhenCreated = DateTime.Now.AddSeconds(-30);
            dbContext.TwitchToken.Update(token);

            refreshToken.AccessToken = result.AccessToken;
            refreshToken.ExpiresIn = TimeSpan.FromSeconds(result.ExpiresIn);
            refreshToken.RefreshToken = result.RefreshToken;
            refreshToken.WhenCreated = DateTime.Now.AddSeconds(-30);

            Token = refreshToken;
            api.Settings.AccessToken = result.AccessToken;

            await dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception e)
        {
            logger.LogException(e);
            return false;
        }
    }

    public async Task ApplyNewTokenAsync(string accessToken, string refreshToken, int expiresIn)
    {
        await using AppDbContext dbContext = await factory.CreateDbContextAsync();

        if (await dbContext.TwitchToken.AsNoTracking().AnyAsync())
        {
            var token = await dbContext.TwitchToken.AsNoTracking().SingleAsync();

            token.AccessToken = accessToken;
            token.RefreshToken = refreshToken;
            token.ExpiresIn = TimeSpan.FromSeconds(expiresIn);
            token.WhenCreated = DateTime.Now.AddSeconds(-30);

            Token = token;

            dbContext.Update(Token);
        }
        else
        {
            var tokenInfo = new TokenInfo
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = TimeSpan.FromSeconds(expiresIn),
                WhenCreated = DateTime.Now.AddSeconds(-30),
            };

            await dbContext.AddAsync(tokenInfo);

            Token = tokenInfo;
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<TokenInfo?> EnsureActualTokenAsync(
        CancellationToken cancellationToken = default
    )
    {
        TokenInfo? result;

        try
        {
            var token = await GetTokenAsync(cancellationToken);

            if (token == null)
            {
                logger.LogWarning("Токен не найден в базе данных");
                result = null;
            }
            else
            {
                var timeUntilExpiry = token.WhenExpires - DateTime.Now;

                if (timeUntilExpiry > TimeSpan.FromMinutes(5))
                {
                    Token = token;
                    logger.LogInformation(
                        "Токен актуален, истекает через {TimeUntilExpiry}",
                        timeUntilExpiry
                    );
                    result = token;
                }
                else
                {
                    logger.LogInformation(
                        "Токен истекает через {TimeUntilExpiry}, обновляем...",
                        timeUntilExpiry.Negate()
                    );
                    var refreshResult = await RefreshTokenAsync(token);

                    if (refreshResult)
                    {
                        logger.LogInformation("Токен успешно обновлен");
                        result = token;
                    }
                    else
                    {
                        logger.LogError("Не удалось обновить токен");
                        result = token;
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogException(e);
            result = Token;
        }

        return result;
    }

    private async Task<TokenInfo> GetFirstTokenAsync()
    {
        await SemaphoreSlim.WaitAsync();
        var token = _tokenInfo;
        if (token == null)
        {
            token = await GetTokenAsync(CancellationToken.None);

            if (token == null)
            {
                await notification.NotifyStreamerAboutAuthAsync(api).ConfigureAwait(false);
                SemaphoreSlim.Release();
                throw new NullReferenceException(nameof(TokenInfo) + " was null");
            }

            await RefreshTokenAsync(token);
        }

        SemaphoreSlim.Release();

        return token;
    }
}
