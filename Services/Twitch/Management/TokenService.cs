using MARS.Server.Services.Twitch.Management.Entitys;

namespace MARS.Server.Services.Twitch.Management;

public class TokenService(
    ITwitchAPI api,
    ILogger<TokenService> logger,
    IDbContextFactory<AppDbContext> factory
)
{
    private TokenInfo? _tokenInfo;

    public TokenInfo? Token
    {
        get => _tokenInfo;
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

            var result = await api.Auth.RefreshAuthTokenAsync(
                refreshToken.RefreshToken,
                api.Settings.Secret,
                api.Settings.ClientId
            );

            var token = (await dbContext.TwitchToken.AsNoTracking().Where(e => true).ToListAsync())[
                0
            ];

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
            TokenInfo token = await dbContext.TwitchToken.AsNoTracking().SingleAsync();

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
}
