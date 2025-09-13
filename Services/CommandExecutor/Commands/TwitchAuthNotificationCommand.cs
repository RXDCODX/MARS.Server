using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Management;
using TwitchLib.Api.Core.Interfaces;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TwitchAuthNotificationCommand(
    TelegramTokenNotification telegramNotification,
    ITwitchAPI twitchApi
) : BaseCommand
{
    public override string CommandName => "twitchauthnotify";
    public override string Description =>
        "Отправляет уведомление о необходимости авторизации в Twitch";
    public override bool IsAdminCommand => true;
    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Вызываем метод уведомления
            await telegramNotification.NotifyStreamerAboutAuthAsync(twitchApi);

            return "✅ Уведомление о необходимости авторизации в Twitch отправлено администраторам";
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка при отправке уведомления: {ex.Message}";
        }
    }
}
