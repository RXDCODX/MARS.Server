using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.StreamManagement;

namespace MARS.Server.Services.CommandExecutor.Commands;

/// <summary>
/// Команда для получения текущего названия трансляции Twitch
/// </summary>
public class GetStreamTitleCommand(
    TwitchStreamManagementService streamManagementService,
    ITwitchClient client,
    ILogger<GetStreamTitleCommand> logger
) : BaseCommand
{
    public override string CommandName => "currenttitle";
    public override string Description => "Показать текущее название трансляции Twitch";
    public override bool IsAdminCommand => false;
    public override Platform[] AvailablePlatforms =>
        [Platform.Twitch, Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!streamManagementService.IsServiceAvailable())
            {
                return GetErrorMessage(platform, "Сервис управления трансляцией недоступен");
            }

            var currentTitle = await streamManagementService.GetCurrentTitleAsync();

            if (string.IsNullOrWhiteSpace(currentTitle))
            {
                return GetErrorMessage(platform, "Не удалось получить текущее название трансляции");
            }

            var message = $"Текущее название трансляции: {currentTitle}";

            // Если команда выполнена через Twitch, отправляем сообщение в чат
            if (platform == Platform.Twitch)
            {
                await client.SendMessageToMainTwitchAsync(message, logger);
            }

            return message;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return GetErrorMessage(platform, "Произошла ошибка при получении названия трансляции");
        }
    }

    private static string GetErrorMessage(Platform platform, string message)
    {
        return platform switch
        {
            Platform.Twitch => $"@{message}",
            Platform.Telegram => $"❌ {message}",
            Platform.Api => $"Error: {message}",
            _ => message,
        };
    }
}

