using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.ApplicationState;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace MARS.Server.Services.SoundRequest.Spotify;

public class SpotifyAuthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<SpotifySoundRequestConfiguration> spotifyOptions,
    ILogger<SpotifyAuthService> logger
)
{
    private const string SpotifyAuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string SpotifyTokenUrl = "https://accounts.spotify.com/api/token";
    private const string SpotifyMeUrl = "https://api.spotify.com/v1/me";

    private readonly SpotifySoundRequestConfiguration _spotifyConfiguration = spotifyOptions.Value;

    private static readonly string[] Scopes =
    [
        "user-read-private",
        "user-read-email",
        "user-read-playback-state",
        "user-modify-playback-state",
    ];

    public async Task<SpotifyAuthCredentials> GetCredentialsAsync(CancellationToken ct)
    {
        var result = new SpotifyAuthCredentials();

        var clientId = await GetRootStateValueAsync(RootStateKeys.SoundRequestSpotifyClientId, ct);
        var clientSecret = await GetRootStateValueAsync(
            RootStateKeys.SoundRequestSpotifyClientSecret,
            ct
        );
        var refreshToken = await GetRootStateValueAsync(
            RootStateKeys.SoundRequestSpotifyRefreshToken,
            ct
        );
        var accessToken = await GetRootStateValueAsync(
            RootStateKeys.SoundRequestSpotifyAccessToken,
            ct
        );
        var expiresAtRaw = await GetRootStateValueAsync(
            RootStateKeys.SoundRequestSpotifyAccessTokenExpiresAtUtc,
            ct
        );
        var deviceIdFromState = await GetRootStateValueAsync(
            RootStateKeys.SoundRequestSpotifyDeviceId,
            ct
        );

        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = _spotifyConfiguration.ClientId;
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            clientSecret = _spotifyConfiguration.ClientSecret;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = _spotifyConfiguration.RefreshToken;
        }

        var expiresAtUtc = DateTime.UnixEpoch;
        if (
            !string.IsNullOrWhiteSpace(expiresAtRaw)
            && DateTime.TryParse(expiresAtRaw, out var parsedExpiresAt)
        )
        {
            expiresAtUtc = DateTime.SpecifyKind(parsedExpiresAt, DateTimeKind.Utc);
        }

        result = new SpotifyAuthCredentials
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            RefreshToken = refreshToken,
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = expiresAtUtc,
            DeviceId = string.IsNullOrWhiteSpace(deviceIdFromState)
                ? _spotifyConfiguration.DeviceId
                : deviceIdFromState,
        };

        return result;
    }

    public async Task<bool> SaveDeviceIdAsync(string? deviceId, CancellationToken ct)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            await UpsertRootStateAsync(
                RootStateKeys.SoundRequestSpotifyDeviceId,
                deviceId,
                "Spotify device ID для SoundRequest",
                "string",
                ct
            );
            result = true;
        }

        return result;
    }

    public async Task<SpotifyAuthStartResult> StartAuthorizationAsync(
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken ct
    )
    {
        var result = new SpotifyAuthStartResult
        {
            Success = false,
            Message = "Не удалось подготовить авторизацию Spotify",
        };

        if (
            !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(clientSecret)
            && !string.IsNullOrWhiteSpace(redirectUri)
        )
        {
            var state = Guid.NewGuid().ToString("N");
            var normalizedRedirectUri = redirectUri.Trim();

            await UpsertRootStateAsync(
                RootStateKeys.SoundRequestSpotifyClientId,
                clientId.Trim(),
                "Spotify ClientId для SoundRequest",
                "string",
                ct
            );
            await UpsertRootStateAsync(
                RootStateKeys.SoundRequestSpotifyClientSecret,
                clientSecret.Trim(),
                "Spotify ClientSecret для SoundRequest",
                "string",
                ct
            );
            await UpsertRootStateAsync(
                RootStateKeys.SoundRequestSpotifyOAuthState,
                state,
                "Spotify OAuth state для SoundRequest",
                "string",
                ct
            );

            // Сохраняем redirectUri, чтобы при callback использовать точно такой же URL при обмене кода на токен
            await UpsertRootStateAsync(
                RootStateKeys.SoundRequestSpotifyRedirectUri,
                normalizedRedirectUri,
                "Spotify OAuth redirect URI для SoundRequest",
                "string",
                ct
            );

            var scope = string.Join(' ', Scopes);
            var authUrl =
                $"{SpotifyAuthorizeUrl}?response_type=code"
                + $"&client_id={Uri.EscapeDataString(clientId.Trim())}"
                + $"&scope={Uri.EscapeDataString(scope)}"
                + $"&state={Uri.EscapeDataString(state)}"
                + "&show_dialog=true"
                + $"&redirect_uri={normalizedRedirectUri}";

            result = new SpotifyAuthStartResult
            {
                Success = true,
                Message = "Ссылка на авторизацию Spotify подготовлена",
                AuthUrl = authUrl,
                State = state,
            };
        }
        else
        {
            result = new SpotifyAuthStartResult
            {
                Success = false,
                Message = "ClientId, ClientSecret и redirectUri обязательны",
            };
        }

        return result;
    }

    public async Task<SpotifyAuthCompleteResult> CompleteAuthorizationAsync(
        string code,
        string state,
        string redirectUri,
        CancellationToken ct
    )
    {
        var result = new SpotifyAuthCompleteResult
        {
            Success = false,
            Message = "Не удалось завершить авторизацию Spotify",
        };

        if (
            !string.IsNullOrWhiteSpace(code)
            && !string.IsNullOrWhiteSpace(state)
            && !string.IsNullOrWhiteSpace(redirectUri)
        )
        {
            var storedState = await GetRootStateValueAsync(
                RootStateKeys.SoundRequestSpotifyOAuthState,
                ct
            );

            if (string.Equals(storedState, state, StringComparison.Ordinal))
            {
                var credentials = await GetCredentialsAsync(ct);

                if (
                    !string.IsNullOrWhiteSpace(credentials.ClientId)
                    && !string.IsNullOrWhiteSpace(credentials.ClientSecret)
                )
                {
                    try
                    {
                        // Попробуем взять redirectUri, сохранённый при старте авторизации
                        var storedRedirect = await GetRootStateValueAsync(
                            RootStateKeys.SoundRequestSpotifyRedirectUri,
                            ct
                        );

                        var token = await RequestAuthorizationTokenAsync(
                            credentials.ClientId,
                            credentials.ClientSecret,
                            code,
                            string.IsNullOrWhiteSpace(storedRedirect)
                                ? redirectUri
                                : storedRedirect,
                            ct
                        );

                        if (!string.IsNullOrWhiteSpace(token?.AccessToken))
                        {
                            await PersistTokenStateAsync(token, ct);

                            var profile = await GetProfileAsync(token.AccessToken, ct);
                            await PersistProfileAsync(profile, ct);

                            result = new SpotifyAuthCompleteResult
                            {
                                Success = true,
                                Message = "Spotify аккаунт подключен",
                                DisplayName = profile?.DisplayName,
                                Product = profile?.Product,
                            };
                        }
                        else
                        {
                            result = new SpotifyAuthCompleteResult
                            {
                                Success = false,
                                Message = "Spotify не вернул access token",
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Ошибка завершения Spotify OAuth");
                        result = new SpotifyAuthCompleteResult
                        {
                            Success = false,
                            Message = "Ошибка обмена OAuth-кода на токен Spotify",
                        };
                    }
                }
                else
                {
                    result = new SpotifyAuthCompleteResult
                    {
                        Success = false,
                        Message = "Не найдены ClientId/ClientSecret для Spotify",
                    };
                }
            }
            else
            {
                result = new SpotifyAuthCompleteResult
                {
                    Success = false,
                    Message = "Проверка OAuth state не пройдена",
                };
            }
        }
        else
        {
            result = new SpotifyAuthCompleteResult
            {
                Success = false,
                Message = "Code, state и redirectUri обязательны",
            };
        }

        return result;
    }

    public async Task<SpotifyAuthStatusResult> GetStatusAsync(CancellationToken ct)
    {
        var result = new SpotifyAuthStatusResult
        {
            IsLinked = false,
            HasClientCredentials = false,
            Message = "Spotify не подключен",
        };

        var credentials = await GetCredentialsAsync(ct);
        var displayName = await GetRootStateValueAsync(
            RootStateKeys.SoundRequestSpotifyDisplayName,
            ct
        );
        var userId = await GetRootStateValueAsync(RootStateKeys.SoundRequestSpotifyUserId, ct);
        var avatarUrl = await GetRootStateValueAsync(
            RootStateKeys.SoundRequestSpotifyAvatarUrl,
            ct
        );
        var product = await GetRootStateValueAsync(RootStateKeys.SoundRequestSpotifyProduct, ct);

        var hasClientCredentials =
            !string.IsNullOrWhiteSpace(credentials.ClientId)
            && !string.IsNullOrWhiteSpace(credentials.ClientSecret);
        var isLinked = hasClientCredentials && !string.IsNullOrWhiteSpace(credentials.RefreshToken);

        result = new SpotifyAuthStatusResult
        {
            IsLinked = isLinked,
            HasClientCredentials = hasClientCredentials,
            DisplayName = displayName,
            UserId = userId,
            AvatarUrl = avatarUrl,
            Product = product,
            DeviceId = credentials.DeviceId,
            AccessTokenExpiresAtUtc =
                credentials.AccessTokenExpiresAtUtc > DateTime.UnixEpoch
                    ? credentials.AccessTokenExpiresAtUtc
                    : null,
            Message = isLinked ? "Spotify подключен" : "Spotify не подключен",
        };

        return result;
    }

    public async Task<bool> DisconnectAsync(CancellationToken ct)
    {
        var result = true;

        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyRefreshToken,
            string.Empty,
            "Spotify refresh token для SoundRequest",
            "string",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyAccessToken,
            string.Empty,
            "Spotify access token для SoundRequest",
            "string",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyAccessTokenExpiresAtUtc,
            string.Empty,
            "Время истечения Spotify access token для SoundRequest",
            "datetime",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyDisplayName,
            string.Empty,
            "Имя подключенного Spotify аккаунта",
            "string",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyUserId,
            string.Empty,
            "ID подключенного Spotify аккаунта",
            "string",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyAvatarUrl,
            string.Empty,
            "Аватар подключенного Spotify аккаунта",
            "string",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyProduct,
            string.Empty,
            "Тип Spotify аккаунта",
            "string",
            ct
        );

        return result;
    }

    public async Task<SpotifyAccessTokenResult> GetValidAccessTokenAsync(CancellationToken ct)
    {
        var result = new SpotifyAccessTokenResult
        {
            Success = false,
            Message = "Не удалось получить Spotify access token",
        };

        var credentials = await GetCredentialsAsync(ct);

        if (
            !string.IsNullOrWhiteSpace(credentials.ClientId)
            && !string.IsNullOrWhiteSpace(credentials.ClientSecret)
            && !string.IsNullOrWhiteSpace(credentials.RefreshToken)
        )
        {
            if (
                !string.IsNullOrWhiteSpace(credentials.AccessToken)
                && credentials.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddSeconds(30)
            )
            {
                result = new SpotifyAccessTokenResult
                {
                    Success = true,
                    Message = "Используется сохраненный Spotify access token",
                    AccessToken = credentials.AccessToken,
                    ExpiresAtUtc = credentials.AccessTokenExpiresAtUtc,
                    DeviceId = credentials.DeviceId,
                };
            }
            else
            {
                try
                {
                    var refreshed = await RefreshAccessTokenAsync(
                        credentials.ClientId,
                        credentials.ClientSecret,
                        credentials.RefreshToken,
                        ct
                    );

                    if (!string.IsNullOrWhiteSpace(refreshed?.AccessToken))
                    {
                        await PersistTokenStateAsync(refreshed, ct);
                        var updated = await GetCredentialsAsync(ct);

                        result = new SpotifyAccessTokenResult
                        {
                            Success = true,
                            Message = "Spotify access token обновлен",
                            AccessToken = updated.AccessToken,
                            ExpiresAtUtc = updated.AccessTokenExpiresAtUtc,
                            DeviceId = updated.DeviceId,
                        };
                    }
                    else
                    {
                        result = new SpotifyAccessTokenResult
                        {
                            Success = false,
                            Message = "Spotify не вернул access token при refresh",
                        };
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка обновления Spotify access token");
                    result = new SpotifyAccessTokenResult
                    {
                        Success = false,
                        Message = "Ошибка обновления Spotify токена",
                    };
                }
            }
        }
        else
        {
            result = new SpotifyAccessTokenResult
            {
                Success = false,
                Message = "Spotify не настроен: нужны ClientId, ClientSecret и RefreshToken",
            };
        }

        return result;
    }

    private async Task PersistTokenStateAsync(SpotifyTokenResponseDto token, CancellationToken ct)
    {
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn - 30));

        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyAccessToken,
            token.AccessToken ?? string.Empty,
            "Spotify access token для SoundRequest",
            "string",
            ct
        );

        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            await UpsertRootStateAsync(
                RootStateKeys.SoundRequestSpotifyRefreshToken,
                token.RefreshToken,
                "Spotify refresh token для SoundRequest",
                "string",
                ct
            );
        }

        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyAccessTokenExpiresAtUtc,
            expiresAtUtc.ToString("O"),
            "Время истечения Spotify access token для SoundRequest",
            "datetime",
            ct
        );
    }

    private async Task PersistProfileAsync(SpotifyProfileDto? profile, CancellationToken ct)
    {
        var displayName = profile?.DisplayName ?? string.Empty;
        var userId = profile?.Id ?? string.Empty;
        var avatarUrl = profile?.Images?.FirstOrDefault()?.Url ?? string.Empty;
        var product = profile?.Product ?? string.Empty;

        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyDisplayName,
            displayName,
            "Имя подключенного Spotify аккаунта",
            "string",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyUserId,
            userId,
            "ID подключенного Spotify аккаунта",
            "string",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyAvatarUrl,
            avatarUrl,
            "Аватар подключенного Spotify аккаунта",
            "string",
            ct
        );
        await UpsertRootStateAsync(
            RootStateKeys.SoundRequestSpotifyProduct,
            product,
            "Тип Spotify аккаунта",
            "string",
            ct
        );
    }

    private async Task<SpotifyTokenResponseDto?> RequestAuthorizationTokenAsync(
        string clientId,
        string clientSecret,
        string code,
        string redirectUri,
        CancellationToken ct
    )
    {
        try
        {
            var oauth = new OAuthClient();
            var token = await oauth.RequestToken(
                new AuthorizationCodeTokenRequest(
                    clientId,
                    clientSecret,
                    code,
                    new Uri(redirectUri)
                )
            );

            return new SpotifyTokenResponseDto
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresIn = token.ExpiresIn,
            };
        }
        catch (APIException ex)
        {
            logger.LogWarning(ex, "Spotify OAuth token error");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка выполнения запроса токена Spotify");
            return null;
        }
    }

    private async Task<SpotifyTokenResponseDto?> RefreshAccessTokenAsync(
        string clientId,
        string clientSecret,
        string refreshToken,
        CancellationToken ct
    )
    {
        try
        {
            var oauth = new OAuthClient();
            var token = await oauth.RequestToken(
                new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken)
            );

            return new SpotifyTokenResponseDto
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresIn = token.ExpiresIn,
            };
        }
        catch (APIException ex)
        {
            logger.LogWarning(ex, "Spotify OAuth refresh error");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обновления Spotify токена");
            return null;
        }
    }

    private async Task<SpotifyProfileDto?> GetProfileAsync(string accessToken, CancellationToken ct)
    {
        SpotifyProfileDto? result = null;

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var spotify = new SpotifyClient(accessToken);
                var profile = await spotify.UserProfile.Current(ct);

                result = new SpotifyProfileDto
                {
                    Id = profile.Id,
                    DisplayName = profile.DisplayName,
                    Images = profile
                        .Images?.Select(i => new SpotifyProfileImageDto { Url = i.Url })
                        .ToList(),
                    Product = null,
                };
            }
            catch (APIException ex)
            {
                logger.LogWarning(ex, "Spotify API profile error");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка получения профиля Spotify");
            }
        }

        return result;
    }

    private async Task<string> GetRootStateValueAsync(string name, CancellationToken ct)
    {
        var result = string.Empty;

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var item = await db.RootState.AsNoTracking().SingleOrDefaultAsync(s => s.Name == name, ct);

        if (item != null)
        {
            result = item.Value ?? string.Empty;
        }

        return result;
    }

    private async Task UpsertRootStateAsync(
        string name,
        string value,
        string description,
        string typeDescription,
        CancellationToken ct
    )
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var existing = await db.RootState.SingleOrDefaultAsync(s => s.Name == name, ct);

        if (existing != null)
        {
            existing.Value = value;
            existing.Description = description;
            existing.TypeDescription = typeDescription;
            db.RootState.Update(existing);
        }
        else
        {
            await db.RootState.AddAsync(
                new RootState
                {
                    Name = name,
                    Value = value,
                    Description = description,
                    TypeDescription = typeDescription,
                },
                ct
            );
        }

        await db.SaveChangesAsync(ct);
    }

    private class SpotifyTokenResponseDto
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class SpotifyProfileDto
    {
        public string? Id { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        public List<SpotifyProfileImageDto>? Images { get; set; }

        public string? Product { get; set; }
    }

    private class SpotifyProfileImageDto
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}

public class SpotifyAuthCredentials
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAtUtc { get; set; } = DateTime.UnixEpoch;

    public string DeviceId { get; set; } = string.Empty;
}

public class SpotifyAuthStartResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string AuthUrl { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;
}

public class SpotifyAuthCompleteResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Product { get; set; }
}

public class SpotifyAuthStatusResult
{
    public bool IsLinked { get; set; }

    public bool HasClientCredentials { get; set; }

    public string? DisplayName { get; set; }

    public string? UserId { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Product { get; set; }

    public string? DeviceId { get; set; }

    public DateTime? AccessTokenExpiresAtUtc { get; set; }

    public string Message { get; set; } = string.Empty;
}

public class SpotifyAccessTokenResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; } = DateTime.UnixEpoch;

    public string DeviceId { get; set; } = string.Empty;
}
