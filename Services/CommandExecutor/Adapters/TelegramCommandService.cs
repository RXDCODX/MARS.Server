using MARS.Server.Services.CommandExecutor.Entitys;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.CommandExecutor.Adapters;

/// <summary>
/// Сервис для обработки команд в Telegram
/// </summary>
public class TelegramCommandService(
    CommandExecutorService executor,
    ICommandService commandService,
    ITelegramBotClient botClient,
    ILogger<TelegramCommandService> logger
) : PlatformCommandServiceBase<long>
{
    public override Platform Platform => Platform.Telegram;

    protected override int DefaultMaxResponseLength => 4096;

    public override IEnumerable<string> UserCommands =>
        executor.GetUserCommandsAsync(Platform.Telegram).GetAwaiter().GetResult();

    public override IEnumerable<string> AdminCommands =>
        executor.GetAdminCommandsAsync(Platform.Telegram).GetAwaiter().GetResult();

    public override Func<long, bool> IsAdmin { get; } = x => x.Equals(TelegramExstension.Rxdcodx);

    /// <summary>
    /// Проверить, доступна ли команда на платформе Telegram
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда доступна</returns>
    public override bool IsCommandAvailable(string commandName)
    {
        // Используем CommandExecutorService для проверки доступности команды
        return executor
            .IsCommandAvailableAsync(commandName, Platform.Telegram)
            .GetAwaiter()
            .GetResult();
    }

    public override string ValidateResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return response;
        }

        var maxLength = GetMaxResponseLength();

        if (response.Length <= maxLength)
        {
            return response;
        }

        // Для Telegram используем более аккуратную обрезку
        var truncated = response.Substring(0, maxLength - 10);

        // Ищем последний полный символ (для поддержки Unicode)
        var lastNewLine = truncated.LastIndexOf('\n');
        if (lastNewLine > maxLength - 50) // Если последний перенос строки не слишком далеко
        {
            truncated = truncated.Substring(0, lastNewLine);
        }

        return truncated + "\n\n[Сообщение обрезано...]";
    }

    public async Task HandMessage(ITelegramBotClient _, Update update)
    {
        if (update.Type != UpdateType.Message)
        {
            return;
        }

        var message = update.Message;

        if (
            message == null
            || message.Type != MessageType.Text
            || message.Text is not { } messageText
            || !messageText.StartsWith('/')
        )
        {
            return;
        }

        await Task.Factory.StartNew(async () =>
        {
            try
            {
                var commandParts = messageText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0)
                {
                    return;
                }

                var commandName = commandParts[0].Substring(1); // Убираем "/"
                var input = commandParts.Length > 1 ? commandParts[1] : "";

                // Обработка специальной команды commands
                if (commandName.Equals("commands", StringComparison.OrdinalIgnoreCase))
                {
                    var includeAdminCommands = IsUserAdmin(message.Chat.Id);

                    var commandsList = GetCommandsList(
                        message.From?.Id ?? 0,
                        UserCommands,
                        AdminCommands,
                        includeAdminCommands
                    );
                    await SendMessage(message.Chat.Id, commandsList);
                    return;
                }

                // Проверяем, существует ли команда
                var commandInfo = await commandService.GetCommandParametersAsync(commandName);
                if (commandInfo is null)
                {
                    await SendMessage(
                        message.Chat.Id,
                        $"Команда '{commandName}' не найдена. Используйте /commands для списка доступных команд."
                    );
                    return;
                }

                // Проверяем количество обязательных параметров
                var requiredParams = commandInfo.Where(p => p.Required).ToArray();
                var inputParts = string.IsNullOrWhiteSpace(input) ? [] : input.Split(' ');

                if (inputParts.Length < requiredParams.Length)
                {
                    var missingParam = requiredParams[inputParts.Length];
                    await SendMessage(
                        message.Chat.Id,
                        $"Не хватает параметра '{missingParam.Name}'. Использование: /{commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}"
                    );
                    return;
                }

                // Проверяем права доступа для админских команд
                var isAdminCommand = await commandService.IsAdminCommandAsync(commandName);
                if (isAdminCommand && !IsUserAdmin(message.From?.Id ?? 0))
                {
                    await SendMessage(
                        message.Chat.Id,
                        $"Команда '{commandName}' доступна только администраторам."
                    );
                    return;
                }

                // Выполняем команду через новый сервис
                string result;
                try
                {
                    result = await commandService.ExecuteCommandAsync(
                        commandName,
                        input,
                        Platform.Telegram
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Ошибка при выполнении команды '{CommandName}' пользователем '{Username}' (ID: {UserId})",
                        commandName,
                        message.From?.Username ?? "Unknown",
                        message.From?.Id
                    );
                    await SendMessage(
                        message.Chat.Id,
                        $"Ошибка при выполнении команды '{commandName}': {ex.Message}"
                    );
                    return;
                }

                // Отправляем результат
                await SendMessage(message.Chat.Id, result);

                logger.LogInformation(
                    "Команда '{CommandName}' выполнена пользователем '{Username}' (ID: {UserId}) с результатом: {Result}",
                    commandName,
                    message.From?.Username ?? "Unknown",
                    message.From?.Id,
                    result
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при обработке команды Telegram");
                await SendMessage(
                    message.Chat.Id,
                    "Произошла ошибка при выполнении команды. Попробуйте позже."
                );
            }
        });
    }

    private async Task SendMessage(long chatId, string message)
    {
        try
        {
            var validatedMessage = ValidateResponse(message);
            var sentMessage = await botClient.SendMessage(chatId, validatedMessage);

            logger.LogInformation(
                "Сообщение отправлено с id: {SentMessageId}",
                sentMessage.MessageId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке сообщения в Telegram");
        }
    }
}
