using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MARS.Server.Services.Telegram.GooglePhotos;

public class GooglePhotosAuthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<GooglePhotosConfiguration> googlePhotosOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<GooglePhotosAuthService> logger
)
{
    private const string GoogleOAuthTokenUrl = "https://oauth2.googleapis.com/token";
    private const string GooglePhotosScope =
        "https://www.googleapis.com/auth/photoslibrary.appendonly";

    private readonly GooglePhotosConfiguration _config = googlePhotosOptions.Value;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<string> GetAuthorizationUrlAsync(CancellationToken ct)
    {
        var state = Guid.NewGuid().ToString();
        await SaveStateKeyAsync(RootStateKeys.GooglePhotosOAuthState, state, ct);

        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        var parameters = new Dictionary<string, string>
        {
            { "client_id", _config.ClientId },
            { "redirect_uri", _config.RedirectUri },
            { "response_type", "code" },
            { "scope", GooglePhotosScope },
            { "state", state },
            { "prompt", "consent" },
            { "access_type", "offline" },
        };

        var query = string.Join(
            "&",
            parameters.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"
            )
        );

        return $"{authUrl}?{query}";
    }

    public async Task<GooglePhotosTokens?> ExchangeCodeForTokenAsync(
        string code,
        CancellationToken ct
    )
    {
        var result = OperationResult<GooglePhotosTokens>.Bad("Ошибка обмена кода на токены");

        if (string.IsNullOrWhiteSpace(code))
        {
            result = OperationResult<GooglePhotosTokens>.Bad("Код авторизации не предоставлен");
        }
        else
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var requestBody = new Dictionary<string, string>
                {
                    { "client_id", _config.ClientId },
                    { "client_secret", _config.ClientSecret },
                    { "code", code },
                    { "grant_type", "authorization_code" },
                    { "redirect_uri", _config.RedirectUri },
                };

                var response = await httpClient.PostAsync(
                    GoogleOAuthTokenUrl,
                    new FormUrlEncodedContent(requestBody),
                    ct
                );

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var tokens = JsonSerializer.Deserialize<GooglePhotosTokens>(responseContent);

                    if (tokens is not null)
                    {
                        await SaveTokensAsync(tokens, ct);
                        result = OperationResult<GooglePhotosTokens>.Ok(
                            "Токены успешно получены",
                            tokens
                        );
                    }
                    else
                    {
                        result = OperationResult<GooglePhotosTokens>.Bad(
                            "Не удалось десериализовать токены"
                        );
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    logger.LogError("Google OAuth error: {ErrorContent}", errorContent);
                    result = OperationResult<GooglePhotosTokens>.Bad(
                        $"HTTP {response.StatusCode}: {errorContent}"
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при обмене кода на токены");
                result = OperationResult<GooglePhotosTokens>.Bad($"Исключение: {ex.Message}");
            }
        }

        return result.Success ? result.Data : null;
    }

    public async Task<string?> GetValidAccessTokenAsync(CancellationToken ct)
    {
        var result = "";

        var accessToken = await GetStateValueAsync(RootStateKeys.GooglePhotosAccessToken, ct);
        var expiresAtRaw = await GetStateValueAsync(
            RootStateKeys.GooglePhotosAccessTokenExpiresAtUtc,
            ct
        );

        if (
            !string.IsNullOrWhiteSpace(accessToken)
            && DateTime.TryParse(expiresAtRaw, out var expiresAt)
        )
        {
            expiresAt = DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc);

            if (DateTime.UtcNow < expiresAt)
            {
                result = accessToken;
            }
            else
            {
                // Токен истёк, пытаемся обновить
                var newTokensResult = await RefreshAccessTokenAsync(ct);
                if (newTokensResult is not null)
                {
                    result = newTokensResult.AccessToken;
                }
            }
        }

        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    public async Task<GooglePhotosTokens?> RefreshAccessTokenAsync(CancellationToken ct)
    {
        var result = OperationResult<GooglePhotosTokens>.Bad("Ошибка обновления токена");

        var refreshToken = await GetStateValueAsync(RootStateKeys.GooglePhotosRefreshToken, ct);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            result = OperationResult<GooglePhotosTokens>.Bad("Refresh Token не найден");
        }
        else
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var requestBody = new Dictionary<string, string>
                {
                    { "client_id", _config.ClientId },
                    { "client_secret", _config.ClientSecret },
                    { "refresh_token", refreshToken },
                    { "grant_type", "refresh_token" },
                };

                var response = await httpClient.PostAsync(
                    GoogleOAuthTokenUrl,
                    new FormUrlEncodedContent(requestBody),
                    ct
                );

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var tokens = JsonSerializer.Deserialize<GooglePhotosTokens>(responseContent);

                    if (tokens is not null)
                    {
                        // Обновляем только AccessToken, RefreshToken остаётся прежним
                        tokens.RefreshToken ??= refreshToken;
                        await SaveTokensAsync(tokens, ct);
                        result = OperationResult<GooglePhotosTokens>.Ok(
                            "Токен успешно обновлён",
                            tokens
                        );
                    }
                    else
                    {
                        result = OperationResult<GooglePhotosTokens>.Bad(
                            "Не удалось десериализовать обновленный токен"
                        );
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    logger.LogError("Google refresh token error: {ErrorContent}", errorContent);
                    result = OperationResult<GooglePhotosTokens>.Bad(
                        $"HTTP {response.StatusCode}: {errorContent}"
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при обновлении токена");
                result = OperationResult<GooglePhotosTokens>.Bad($"Исключение: {ex.Message}");
            }
        }

        return result.Success ? result.Data : null;
    }

    public async Task<bool> IsAuthorizedAsync(CancellationToken ct)
    {
        var token = await GetValidAccessTokenAsync(ct);
        return !string.IsNullOrWhiteSpace(token);
    }

    private async Task SaveTokensAsync(GooglePhotosTokens tokens, CancellationToken ct)
    {
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn - 60);

        await SaveStateKeyAsync(RootStateKeys.GooglePhotosAccessToken, tokens.AccessToken, ct);
        await SaveStateKeyAsync(
            RootStateKeys.GooglePhotosAccessTokenExpiresAtUtc,
            expiresAtUtc.ToString("O"),
            ct
        );
        if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            await SaveStateKeyAsync(
                RootStateKeys.GooglePhotosRefreshToken,
                tokens.RefreshToken,
                ct
            );
        }
        await SaveStateKeyAsync(RootStateKeys.GooglePhotosIsAuthorized, "true", ct);
    }

    private async Task SaveStateKeyAsync(string key, string value, CancellationToken ct)
    {
        using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var state = await context.RootState.FindAsync(new object[] { key }, cancellationToken: ct);

        if (state is null)
        {
            state = new RootState { Name = key, Value = value };
            context.RootState.Add(state);
        }
        else
        {
            state.Value = value;
        }

        await context.SaveChangesAsync(ct);
    }

    private async Task<string> GetStateValueAsync(string key, CancellationToken ct)
    {
        using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var state = await context
            .RootState.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == key, cancellationToken: ct);
        return state?.Value ?? string.Empty;
    }
}

public class GooglePhotosTokens
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";
}
