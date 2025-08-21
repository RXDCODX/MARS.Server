using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.CommandExecutor.Adapters;

/// <summary>
/// Сервис для обработки команд в Twitch
/// </summary>
public class TwitchCommandService : PlatformCommandServiceBase<string>, IHostedService
{
    private readonly CommandFactory _commandFactory;
    private readonly ITwitchClient _client;
    private readonly ILogger<TwitchCommandService> _logger;
    private readonly Dictionary<string, BaseCommand> _commands;
    private readonly Dictionary<string, string> _aliases;

    public override Platform Platform => Platform.Twitch;

    protected override int DefaultMaxResponseLength => 500; // Twitch имеет ограничения на длину сообщений

    public override IEnumerable<string> UserCommands =>
        _commands
            .Values.Where(c => !c.IsAdminCommand && c.IsAvailableOnPlatform(Platform.Twitch))
            .Select(c => $"!{c.CommandName}");

    public override IEnumerable<string> AdminCommands =>
        _commands
            .Values.Where(c => c.IsAdminCommand && c.IsAvailableOnPlatform(Platform.Twitch))
            .Select(c => $"!{c.CommandName}");

    public override Func<string, bool> IsAdmin =>
        (userId) => userId.Equals(TwitchExstension.ChannelId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Сервис для обработки команд в Twitch
    /// </summary>
    public TwitchCommandService(
        CommandFactory commandFactory,
        ITwitchClient client,
        IHostApplicationLifetime lifetime,
        ILogger<TwitchCommandService> logger
    )
    {
        _commandFactory = commandFactory;
        _client = client;
        _logger = logger;
        _commands = new Dictionary<string, BaseCommand>(StringComparer.OrdinalIgnoreCase);
        _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        InitializeCommands();

        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            client.OnMessageReceived -= ClientOnOnMessageReceived;
        });
    }

    private void InitializeCommands()
    {
        // Создаем все команды через фабрику
        var allCommands = _commandFactory.CreateAllCommands();

        foreach (var command in allCommands)
        {
            RegisterCommand(command.Value);
        }
    }

    private void RegisterCommand(BaseCommand command)
    {
        _commands[command.CommandName] = command;

        // Добавляем алиасы из свойства Aliases
        foreach (var alias in command.Aliases)
        {
            _aliases[alias] = command.CommandName;
        }

        // Добавляем специальные алиасы
        if (command.CommandName == "framedata")
        {
            _aliases["fd"] = command.CommandName;
        }
    }

    private void ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        Task.Factory.StartNew(async () =>
        {
            try
            {
                var message = e.ChatMessage.Message;
                var username = e.ChatMessage.Username;
                var userId = e.ChatMessage.UserId;

                // Проверяем, что сообщение начинается с команды
                if (!message.StartsWith('!'))
                {
                    return;
                }

                // Разбираем команду
                var commandParts = message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0)
                {
                    return;
                }

                var commandName = commandParts[0].Substring(1); // Убираем "!"
                var input = commandParts.Length > 1 ? commandParts[1] : "";

                // Обработка специальной команды commands
                if (commandName.Equals("commands", StringComparison.OrdinalIgnoreCase))
                {
                    var includeAdminCommands = IsAdmin.Invoke(userId);

                    var commandsList = GetCommandsList(
                        userId,
                        UserCommands,
                        AdminCommands,
                        includeAdminCommands
                    );
                    await SendMessage(commandsList);
                    return;
                }

                // Проверяем, существует ли команда
                if (!_commands.TryGetValue(commandName, out var command))
                {
                    if (!_aliases.TryGetValue(commandName, out var commandAlias))
                    {
                        return;
                    }
                    else
                    {
                        command = _commands[commandAlias];
                    }
                }

                // Проверяем количество обязательных параметров
                var commandInfo = command.GetParameterInfo();
                var requiredParams = commandInfo.Where(p => p.Required).ToArray();
                var inputParts = string.IsNullOrWhiteSpace(input) ? [] : input.Split(' ');

                if (inputParts.Length < requiredParams.Length)
                {
                    var missingParam = requiredParams[inputParts.Length];
                    await SendMessage(
                        $"Не хватает параметра '{missingParam.Name}'. Использование: !{commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}"
                    );
                    return;
                }

                // Проверяем права доступа для админских команд
                if (command.IsAdminCommand && !IsUserAdmin(userId))
                {
                    await SendMessage($"Команда '{commandName}' доступна только администраторам.");
                    return;
                }

                // Выполняем команду
                string result;
                try
                {
                    // Разбираем параметры из входной строки
                    var parameters = command.ParseParameters(input);

                    // Выполняем команду
                    result = await command.ExecuteAsync(parameters, Platform.Twitch);

                    // Валидируем ответ для платформы
                    result = ValidateResponse(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Ошибка при выполнении команды '{CommandName}' пользователем '{Username}'",
                        commandName,
                        username
                    );
                    await SendMessage(
                        $"Ошибка при выполнении команды '{commandName}': {ex.Message}"
                    );
                    return;
                }

                // Отправляем результат
                await SendMessage(result);

                _logger.LogInformation(
                    "Команда '{CommandName}' выполнена пользователем '{Username}' с результатом: {Result}",
                    commandName,
                    username,
                    result
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке команды Twitch");
                await SendMessage("Произошла ошибка при выполнении команды. Попробуйте позже.");
            }
        });
    }

    private async Task SendMessage(string message)
    {
        try
        {
            var validatedMessage = ValidateResponse(message);
            await _client.SendMessageToMainTwitchAsync(validatedMessage, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке сообщения в Twitch");
        }
    }

    /// <summary>
    /// Проверить, доступна ли команда на платформе Twitch
    /// </summary>
    /// <param name="commandName">Название команды</param>
    /// <returns>True если команда доступна</returns>
    public override bool IsCommandAvailable(string commandName)
    {
        // Проверяем алиасы
        if (_aliases.TryGetValue(commandName, out var actualCommandName))
        {
            commandName = actualCommandName;
        }

        return _commands.TryGetValue(commandName, out var command)
            && command.IsAvailableOnPlatform(Platform.Twitch);
    }

    /// <summary>
    /// Валидировать ответ для платформы Twitch
    /// </summary>
    /// <param name="response">Ответ команды</param>
    /// <returns>Валидный ответ</returns>
    public override string ValidateResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return response;
        }

        var maxLength = GetMaxResponseLength();

        response = response.Replace("\n", " ");

        if (response.Length <= maxLength)
        {
            return response;
        }

        // Для Twitch используем более короткую обрезку
        var truncated = response.Substring(0, maxLength - 5);
        return truncated + "...";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
