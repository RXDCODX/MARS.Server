using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, BaseCommand> _commands;
    private readonly Dictionary<string, string> _aliases;

    public override Platform Platform => Platform.Twitch;

    protected override int DefaultMaxResponseLength => 500; // Twitch имеет ограничения на длину сообщений

    public override char[] CommandPrefixes => ['!'];

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
        IServiceProvider serviceProviderProvider,
        IHostApplicationLifetime lifetime,
        ILogger<TwitchCommandService> logger
    )
    {
        _commandFactory = commandFactory;
        _client = client;
        _serviceProvider = serviceProviderProvider;
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
    }

    private void ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (
            TwitchExstension.BlackList.All(t =>
                !t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var message = e.ChatMessage.Message;
                    var username = e.ChatMessage.Username;
                    var userId = e.ChatMessage.UserId;

                    // Проверяем, что сообщение начинается с префикса команды
                    if (!StartsWithCommandPrefix(message))
                    {
                        return;
                    }

                    // Разбираем команду
                    var commandParts = message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (commandParts.Length == 0)
                    {
                        return;
                    }

                    var commandName = TrimCommandPrefix(commandParts[0]);
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

                    // Обработка специальной команды c (краткий список)
                    if (commandName.Equals("c", StringComparison.OrdinalIgnoreCase))
                    {
                        var includeAdminCommands = IsAdmin.Invoke(userId);

                        var shortCommandsList = GetShortCommandsList(userId, includeAdminCommands);
                        await SendMessage(shortCommandsList);
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
                    var inputParts = string.IsNullOrWhiteSpace(input)
                        ? []
                        : BaseCommand.ParseParametersWithQuotes(input);

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
                        await SendMessage(
                            $"Команда '{commandName}' доступна только администраторам."
                        );
                        return;
                    }

                    // Проверяем доступность команды для платформы Twitch
                    if (!command.IsAvailableOnPlatform(Platform.Twitch))
                    {
                        await SendMessage(
                            $"Команда '{commandName}' недоступна на платформе Twitch."
                        );
                        return;
                    }

                    // Выполняем команду
                    string result;
                    try
                    {
                        var parameters = command.ParseParameters(input);

                        // Гарантируем наличие пользователя в БД и добавляем в параметры
                        var userEnsureService = _serviceProvider
                            .CreateAsyncScope()
                            .ServiceProvider.GetRequiredService<TwitchUserEnsureService>();
                        var twitchUser = await userEnsureService.EnsureUserExistsAsync(
                            e.ChatMessage
                        );
                        parameters["user"] = twitchUser;

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
    }

    /// <summary>
    /// Получить краткий список команд для пользователя
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="includeAdminCommands">Включить админские команды</param>
    /// <returns>Краткий список команд</returns>
    private string GetShortCommandsList(string userId, bool includeAdminCommands = false)
    {
        var isAdmin = IsUserAdmin(userId);

        if (includeAdminCommands && !isAdmin)
        {
            return "У вас нет прав для просмотра админских команд.";
        }

        var commands = new List<string>();

        // Фильтруем команды по видимости и добавляем только названия
        foreach (
            var command in _commands.Values.Where(c =>
                !c.IsAdminCommand && c.IsAvailableOnPlatform(Platform.Twitch)
            )
        )
        {
            if (command.IsVisibleIn(CommandVisibility.ShortList))
            {
                commands.Add($"!{command.CommandName}");
            }
        }

        // Добавляем админские команды если запрошено и пользователь админ
        if (includeAdminCommands && isAdmin)
        {
            foreach (
                var command in _commands.Values.Where(c =>
                    c.IsAdminCommand && c.IsAvailableOnPlatform(Platform.Twitch)
                )
            )
            {
                if (command.IsVisibleIn(CommandVisibility.ShortList))
                {
                    commands.Add($"!{command.CommandName}");
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
        var result = false;

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            // Проверяем алиасы
            if (_aliases.TryGetValue(commandName, out var actualCommandName))
            {
                commandName = actualCommandName;
            }

            if (_commands.TryGetValue(commandName, out var command))
            {
                result = command.IsAvailableOnPlatform(Platform.Twitch);
            }
        }

        return result;
    }

    /// <summary>
    /// Валидировать ответ для платформы Twitch
    /// </summary>
    /// <param name="response">Ответ команды</param>
    /// <returns>Валидный ответ</returns>
    public override string ValidateResponse(string response)
    {
        var result = response ?? string.Empty;

        if (!string.IsNullOrEmpty(response))
        {
            var maxLength = GetMaxResponseLength();

            response = response.Replace("\n", " ");

            if (response.Length > maxLength)
            {
                // Для Twitch используем более короткую обрезку
                var truncated = response.Substring(0, maxLength - 5);
                result = truncated + "...";
            }
            else
            {
                result = response;
            }
        }

        return result;
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
