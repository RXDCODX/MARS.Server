using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest.Spotify;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SpotifyAuthStartCommand(
    SpotifyAuthService spotifyAuthService,
    IOptions<SpotifySoundRequestConfiguration> spotifyOptions
) : BaseCommand
{
    private readonly SpotifySoundRequestConfiguration _spotifyConfig = spotifyOptions.Value;

    public override string CommandName => "spotifyauthstart";

    public override string Description => "Генерирует ссылку для старта авторизации Spotify";

    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Discord];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "redirectUri",
                Description = "Redirect URI для callback Spotify",
                Type = "string",
                Required = true,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Spotify авторизация недоступна";

        if (
            string.IsNullOrWhiteSpace(_spotifyConfig.ClientId)
            || string.IsNullOrWhiteSpace(_spotifyConfig.ClientSecret)
        )
        {
            result = "Spotify ClientId или ClientSecret не установлены в конфигурации";
        }
        else if (parameters.TryGetValue("redirectUri", out var redirectUriObj))
        {
            var redirectUri = redirectUriObj?.ToString() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(redirectUri))
            {
                try
                {
                    var authResult = await spotifyAuthService.StartAuthorizationAsync(
                        _spotifyConfig.ClientId,
                        _spotifyConfig.ClientSecret,
                        redirectUri,
                        cancellationToken
                    );

                    result = authResult.Success
                        ? $"Spotify авторизация подготовлена.\n\nСсылка:\n{authResult.AuthUrl}\n\nState: {authResult.State}"
                        : authResult.Message;
                }
                catch (Exception ex)
                {
                    result = $"Ошибка при подготовке авторизации Spotify: {ex.Message}";
                }
            }
            else
            {
                result = "RedirectUri не может быть пустым";
            }
        }
        else
        {
            result = "RedirectUri обязателен";
        }

        return result;
    }
}
