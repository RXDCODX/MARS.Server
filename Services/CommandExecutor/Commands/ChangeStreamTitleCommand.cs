using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.StreamManagement;

namespace MARS.Server.Services.CommandExecutor.Commands;

/// <summary>
/// Команда для смены названия трансляции Twitch
/// </summary>
public class ChangeStreamTitleCommand(
    TwitchStreamManagementService streamManagementService,
    ILogger<ChangeStreamTitleCommand> logger
) : BaseCommand
{
    public override string CommandName => "title";
    public override string Description => "Смена названия трансляции Twitch";
    public override bool IsAdminCommand => true;
    public override Platform[] AvailablePlatforms =>
        [Platform.Twitch, Platform.Telegram, Platform.Api];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "новое_название",
                Description = "Новое название для трансляции",
                Type = "string",
                Required = false,
            },
        ];

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

            if (parameters.TryGetValue("новое_название", out var titleParam))
            {
                var newTitle = titleParam.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(newTitle))
                {
                    return GetErrorMessage(platform, "Название трансляции не может быть пустым");
                }

                // Ограничиваем длину названия (Twitch ограничивает до 140 символов)
                if (newTitle.Length > 140)
                {
                    newTitle = newTitle[..140];
                    logger.LogWarning("Название трансляции обрезано до 140 символов");
                }

                var success = await streamManagementService.ChangeStreamTitleAsync(newTitle);

                if (success)
                {
                    var message = $"Название трансляции успешно изменено на: {newTitle}";

                    return message;
                }
                else
                {
                    return GetErrorMessage(platform, "Не удалось изменить название трансляции");
                }
            }
            else
            {
                var answer = await streamManagementService.GetCurrentTitleAsync();
                return string.IsNullOrWhiteSpace(answer)
                    ? "Не удалось получить текущее название трансляции"
                    : "Текущее название трансляции: " + answer;
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            return GetErrorMessage(platform, "Произошла ошибка при смене названия трансляции");
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
