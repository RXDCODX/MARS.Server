namespace MARS.Server.Services.Twitch.StreamManagement;

/// <summary>
/// Сервис для обработки команды !title в Twitch чате (смена и получение названия)
/// </summary>
public class TwitchTitleChangeCommand(
    TwitchStreamManagementService streamManagementService,
    ITwitchClient client,
    ILogger<TwitchTitleChangeCommand> logger
) : BackgroundService
{
    public bool IsServiceActive { get; set; } = true;

    private const string CommandPrefix = "!title";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            client.OnMessageReceived += OnMessageReceived;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= OnMessageReceived;
        await base.StopAsync(cancellationToken);
    }

    private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        if (!IsServiceActive)
        {
            return;
        }

        var message = args.ChatMessage.Message.Trim();
        var username = args.ChatMessage.DisplayName;
        var isModerator = args.ChatMessage.UserDetail.IsModerator;
        var isBroadcaster = args.ChatMessage.IsBroadcaster;

        // Проверяем, что это команда !title
        if (
            message.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase)
            && !TwitchExstension.BlackList.Logins.Any(t =>
                t.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            await Task.Run(async () =>
            {
                try
                {
                    await ProcessTitleCommand(message, username, isModerator, isBroadcaster);
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                }
            });
        }
    }

    private async Task ProcessTitleCommand(
        string message,
        string username,
        bool isModerator,
        bool isBroadcaster
    )
    {
        // Извлекаем параметры из команды
        var parameters = message.Substring(CommandPrefix.Length).Trim();

        // Если параметров нет - показываем текущее название
        if (string.IsNullOrWhiteSpace(parameters))
        {
            await ShowCurrentTitle(username);
            return;
        }

        // Если есть параметры - меняем название (только для модераторов/стримера)
        if (isModerator || isBroadcaster)
        {
            await ChangeTitle(parameters, username);
        }
        else
        {
            await client.SendMessageToMainTwitchAsync(
                $"@{username}, у вас нет прав для изменения названия трансляции. Эта команда доступна только модераторам и стримеру.",
                logger
            );
        }
    }

    private async Task ShowCurrentTitle(string username)
    {
        // Проверяем доступность сервиса
        if (!streamManagementService.IsServiceAvailable())
        {
            await client.SendMessageToMainTwitchAsync(
                $"@{username}, сервис управления трансляцией недоступен. Попробуйте позже.",
                logger
            );
            return;
        }

        // Получаем текущее название трансляции
        var currentTitle = await streamManagementService.GetCurrentTitleAsync();

        if (string.IsNullOrWhiteSpace(currentTitle))
        {
            var errorMessage = $"@{username}, не удалось получить текущее название трансляции.";
            await client.SendMessageToMainTwitchAsync(errorMessage, logger);

            logger.LogWarning(
                "Не удалось получить название трансляции для пользователя {Username}",
                username
            );
            return;
        }

        // Отправляем текущее название в чат
        var successMessage = $"@{username}, текущее название трансляции: {currentTitle}";
        await client.SendMessageToMainTwitchAsync(successMessage, logger);

        logger.LogInformation(
            "Название трансляции показано пользователю {Username}: {CurrentTitle}",
            username,
            currentTitle
        );
    }

    private async Task ChangeTitle(string newTitle, string username)
    {
        // Проверяем доступность сервиса
        if (!streamManagementService.IsServiceAvailable())
        {
            await client.SendMessageToMainTwitchAsync(
                $"@{username}, сервис управления трансляцией недоступен. Попробуйте позже.",
                logger
            );
            return;
        }

        // Ограничиваем длину названия (Twitch ограничивает до 140 символов)
        if (newTitle.Length > 140)
        {
            newTitle = newTitle[..140];
            logger.LogWarning(
                "Название трансляции обрезано до 140 символов пользователем {Username}",
                username
            );
        }

        // Меняем название трансляции
        var success = await streamManagementService.ChangeStreamTitleAsync(newTitle);

        if (success)
        {
            var successMessage =
                $"@{username}, название трансляции успешно изменено на: {newTitle}";
            await client.SendMessageToMainTwitchAsync(successMessage, logger);

            logger.LogInformation(
                "Название трансляции изменено пользователем {Username} на: {NewTitle}",
                username,
                newTitle
            );
        }
        else
        {
            var errorMessage =
                $"@{username}, не удалось изменить название трансляции. Попробуйте позже.";
            await client.SendMessageToMainTwitchAsync(errorMessage, logger);

            logger.LogWarning(
                "Не удалось изменить название трансляции для пользователя {Username}",
                username
            );
        }
    }
}
