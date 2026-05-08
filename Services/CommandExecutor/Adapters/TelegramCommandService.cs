using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.CommandExecutor.Adapters;

/// <summary>
/// Сервис для обработки команд в Telegram
/// </summary>
public class TelegramCommandService(
    ICommandService commandService,
    ITelegramBotClient botClient,
    ILogger<TelegramCommandService> logger
) : PlatformCommandServiceBase<long>
{
    public override Platform Platform => Platform.Telegram;

    protected override int DefaultMaxResponseLength => 4096;

    public override char[] CommandPrefixes => ['/'];

    public override IEnumerable<string> UserCommands =>
        commandService.GetUserCommands(Platform.Telegram);

    public override IEnumerable<string> AdminCommands =>
        commandService.GetAdminCommands(Platform.Telegram);

    public override Func<long, bool> IsAdmin { get; } = x => x.Equals(TelegramExstension.Rxdcodx);

    /// <summary>
    /// Проверить, доступна ли команда на платформе Telegram
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда доступна</returns>
    public virtual bool IsCommandAvailable(string commandName)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            result = commandService.IsCommandAvailable(commandName, Platform.Telegram);
        }

        return result;
    }

    public override string ValidateResponse(string response)
    {
        var result = response ?? string.Empty;

        if (!string.IsNullOrEmpty(response))
        {
            var maxLength = GetMaxResponseLength();

            if (response.Length > maxLength)
            {
                // Для Telegram используем более аккуратную обрезку
                var truncated = response.Substring(0, maxLength - 10);

                // Ищем последний полный символ (для поддержки Unicode)
                var lastNewLine = truncated.LastIndexOf('\n');
                if (lastNewLine > maxLength - 50) // Если последний перенос строки не слишком далеко
                {
                    truncated = truncated.Substring(0, lastNewLine);
                }

                result = truncated + "\n\n[Сообщение обрезано...]";
            }
        }

        return result;
    }

    public async Task HandMessage(ITelegramBotClient _, Update update)
    {
        if (update?.Type == UpdateType.Message)
        {
            var message = update.Message;

            if (
                message is { Type: MessageType.Text, Text: { } messageText }
                && StartsWithCommandPrefix(messageText)
            )
            {
                await Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        var commandParts = messageText.Split(
                            ' ',
                            2,
                            StringSplitOptions.RemoveEmptyEntries
                        );
                        if (commandParts.Length > 0)
                        {
                            var commandName = TrimCommandPrefix(commandParts[0]);
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
                            }
                            // Обработка специальной команды с (краткий список)
                            else if (commandName.Equals("c", StringComparison.OrdinalIgnoreCase))
                            {
                                var includeAdminCommands = IsUserAdmin(message.Chat.Id);

                                var shortCommandsList = GetShortCommandsList(
                                    message.From?.Id ?? 0,
                                    includeAdminCommands
                                );
                                await SendMessage(message.Chat.Id, shortCommandsList);
                            }
                            else
                            {
                                // Проверяем, существует ли команда
                                var commandInfo = commandService.GetCommandParameters(commandName);
                                if (commandInfo is not null)
                                {
                                    // Проверяем количество обязательных параметров
                                    var requiredParams = commandInfo
                                        .Where(p =>
                                            p.Required
                                            && !(
                                                p.Type == nameof(Message)
                                                && p.Name.Equals(
                                                    "message",
                                                    StringComparison.OrdinalIgnoreCase
                                                )
                                            )
                                        )
                                        .ToArray();

                                    var parameters = commandService.ParseParameters(
                                        input,
                                        commandInfo
                                    );

                                    parameters["message"] = message;

                                    if (parameters.Count >= requiredParams.Length)
                                    {
                                        // Проверяем права доступа для админских команд
                                        var isAdminCommand = commandService.IsAdminCommand(
                                            commandName
                                        );
                                        if (!isAdminCommand || IsUserAdmin(message.From?.Id ?? 0))
                                        {
                                            // Выполняем команду через новый сервис
                                            try
                                            {
                                                var result =
                                                    await commandService.ExecuteCommandAsync(
                                                        commandName,
                                                        parameters,
                                                        Platform.Telegram
                                                    );

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
                                            }
                                        }
                                        else
                                        {
                                            await SendMessage(
                                                message.Chat.Id,
                                                $"Команда '{commandName}' доступна только администраторам."
                                            );
                                        }
                                    }
                                    else
                                    {
                                        var missingParam = requiredParams[parameters.Count];
                                        await SendMessage(
                                            message.Chat.Id,
                                            $"Не хватает параметра '{missingParam.Name}'. Использование: /{commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}"
                                        );
                                    }
                                }
                                else
                                {
                                    await SendMessage(
                                        message.Chat.Id,
                                        $"Команда '{commandName}' не найдена. Используйте /commands для списка доступных команд."
                                    );
                                }
                            }
                        }
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
        }
    }

    /// <summary>
    /// Получить краткий список команд для пользователя
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="includeAdminCommands">Включить админские команды</param>
    /// <returns>Краткий список команд</returns>
    private string GetShortCommandsList(long userId, bool includeAdminCommands = false)
    {
        var isAdmin = IsUserAdmin(userId);

        if (includeAdminCommands && !isAdmin)
        {
            return "У вас нет прав для просмотра админских команд.";
        }

        var commands = new List<string>();

        // Получаем команды через CommandExecutorService и фильтруем по видимости
        var allCommands = commandService.GetUserCommandsInfo(Platform.Telegram);
        var adminCommands = commandService.GetAdminCommandsInfo(Platform.Telegram);

        // Фильтруем пользовательские команды по видимости
        foreach (var command in allCommands)
        {
            if (command.IsVisibleIn(CommandVisibility.ShortList))
            {
                commands.Add($"/{command.CommandName}");
            }
        }

        // Добавляем админские команды если запрошено и пользователь админ
        if (includeAdminCommands && isAdmin)
        {
            foreach (var command in adminCommands)
            {
                if (command.IsVisibleIn(CommandVisibility.ShortList))
                {
                    commands.Add($"/{command.CommandName}");
                }
            }
        }

        if (commands.Count == 0)
        {
            return "Нет доступных команд для вашей роли.";
        }

        var result = "Команды: ";

        result += string.Join(" | ", commands);

        return result;
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
