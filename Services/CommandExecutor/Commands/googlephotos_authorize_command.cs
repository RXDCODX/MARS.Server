using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Telegram.GooglePhotos;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class GooglePhotosAuthorizeCommand(
    GooglePhotosAuthService authService,
    IOptions<GooglePhotosConfiguration> googlePhotosOptions
) : BaseCommand
{
    private readonly GooglePhotosConfiguration _config = googlePhotosOptions.Value;

    public override string CommandName => "googlephotosauthorize";

    public override string Description => "Генерирует ссылку для авторизации Google Photos";

    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Discord];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Google Photos авторизация недоступна";

        if (!_config.Enabled)
        {
            result = "Google Photos отключен в конфигурации";
        }
        else if (
            string.IsNullOrWhiteSpace(_config.ClientId)
            || string.IsNullOrWhiteSpace(_config.ClientSecret)
            || string.IsNullOrWhiteSpace(_config.RedirectUri)
        )
        {
            result =
                "Google Photos ClientId, ClientSecret или RedirectUri не установлены в конфигурации";
        }
        else
        {
            try
            {
                var authUrl = await authService.GetAuthorizationUrlAsync(cancellationToken);
                result = $"Google Photos авторизация подготовлена.\n\nСсылка:\n{authUrl}";
            }
            catch (Exception ex)
            {
                result = $"Ошибка при подготовке авторизации Google Photos: {ex.Message}";
            }
        }

        return result;
    }
}
