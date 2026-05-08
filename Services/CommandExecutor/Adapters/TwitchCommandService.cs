using MARS.Server.Services.CommandExecutor;
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
    private readonly ICommandService _commandService;
    private readonly ITwitchClient _client;
    private readonly ILogger<TwitchCommandService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public override Platform Platform => Platform.Twitch;

    protected override int DefaultMaxResponseLength => 500; // Twitch имеет ограничения на длину сообщений

    public override char[] CommandPrefixes => ['!'];

    public override IEnumerable<string> UserCommands =>
        _commandService.GetUserCommands(Platform.Twitch).Select(c => $"!{c}");

    public override IEnumerable<string> AdminCommands =>
        _commandService.GetAdminCommands(Platform.Twitch).Select(c => $"!{c}");

    public override Func<string, bool> IsAdmin =>
        (userId) => userId.Equals(TwitchExstension.ChannelId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Сервис для обработки команд в Twitch
    /// </summary>
    public TwitchCommandService(
        ICommandService commandService,
        ITwitchClient client,
        IServiceProvider serviceProviderProvider,
        IHostApplicationLifetime lifetime,
        ILogger<TwitchCommandService> logger
    )
    {
        _commandService = commandService;
        _client = client;
        _serviceProvider = serviceProviderProvider;
        _logger = logger;

        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += ClientOnOnMessageReceived;
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            client.OnMessageReceived -= ClientOnOnMessageReceived;
        });
    }

    private async Task ClientOnOnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (
            TwitchExstension.BlackList.All(t =>
                !t.Equals(e.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            await Task.Factory.StartNew(async () =>
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

                    // Проверяем, доступна ли команда
                    if (!_commandService.IsCommandAvailable(commandName, Platform.Twitch))
                    {
                        return;
                    }

                    // Получаем описание параметров команды
                    var commandInfo = _commandService.GetCommandParameters(commandName);
                    var requiredParams = (commandInfo ?? Array.Empty<CommandParameterInfo>())
                        .Where(p => p.Required)
                        .ToArray();

                    var inputParts = string.IsNullOrWhiteSpace(input)
                        ? Array.Empty<string>()
                        : BaseCommand.ParseParametersWithQuotes(input);

                    if (inputParts.Length < requiredParams.Length)
                    {
                        var missingParam = requiredParams[inputParts.Length];
                        await SendMessage(
                            $"Не хватает параметра '{missingParam.Name}'. Использование: !{commandName} {string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"))}"
                        );
                        return;
                    }

                    // Проверяем права доступа для админских команд (если параметр показывает admin)
                    if (_commandService.IsAdminCommand(commandName) && !IsUserAdmin(userId))
                    {
                        await SendMessage(
                            $"Команда '{commandName}' доступна только администраторам."
                        );
                        return;
                    }

                    // Выполняем команду через ICommandService, собрав параметры
                    string result;
                    try
                    {
                        var parameters = _commandService.ParseParameters(input, commandInfo);

                        // Гарантируем наличие пользователя в БД и добавляем в параметры
                        var userEnsureService = _serviceProvider
                            .CreateAsyncScope()
                            .ServiceProvider.GetRequiredService<TwitchUserEnsureService>();
                        var twitchUser = await userEnsureService.EnsureUserExistsAsync(
                            e.ChatMessage
                        );
                        parameters["user"] = twitchUser;

                        // Дополнительные контекстные параметры для адаптера
                        parameters["username"] = username;
                        parameters["userId"] = userId;
                        parameters["channel"] = e.ChatMessage.Channel;
                        parameters["rawMessage"] = message;
                        parameters["platform"] = Platform.Twitch;

                        // Выполняем команду через сервис
                        result = await _commandService.ExecuteCommandAsync(
                            commandName,
                            parameters,
                            Platform.Twitch
                        );

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

        var commands = (
            _commandService
                .GetUserCommandsInfo(Platform.Twitch)
                .Where(command => command.IsVisibleIn(CommandVisibility.ShortList))
                .Select(command => $"!{command.CommandName}")
        ).ToList();

        // Фильтруем команды по видимости и добавляем только названия

        // Добавляем админские команды если запрошено и пользователь админ
        if (includeAdminCommands && isAdmin)
        {
            commands.AddRange(
                _commandService
                    .GetAdminCommandsInfo(Platform.Twitch)
                    .Where(command => command.IsVisibleIn(CommandVisibility.ShortList))
                    .Select(command => $"!{command.CommandName}")
            );
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
