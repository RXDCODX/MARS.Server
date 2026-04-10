using DSharpPlus;
using DSharpPlus.EventArgs;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.Discord.PlayRequest;
using ServerDiscordConfiguration = MARS.Server.Configuration.DiscordConfiguration;

namespace MARS.Server.Services.CommandExecutor.Adapters;

public class DiscordCommandService(
    CommandExecutorService executor,
    ICommandService commandService,
    DiscordPlayRequestService discordPlayRequestService,
    IDiscordGatewayService discordGatewayService,
    IOptions<ServerDiscordConfiguration> discordOptions,
    ILogger<DiscordCommandService> logger
) : PlatformCommandServiceBase<ulong>, IHostedService
{
    private readonly ServerDiscordConfiguration _discordConfiguration = discordOptions.Value;
    private bool _isHandlerRegistered;

    public override Platform Platform => Platform.Discord;

    protected override int DefaultMaxResponseLength => 1900;

    public override char[] CommandPrefixes => ['/', '!'];

    public override IEnumerable<string> UserCommands =>
        executor.GetUserCommandsAsync(Platform.Discord).GetAwaiter().GetResult();

    public override IEnumerable<string> AdminCommands =>
        executor.GetAdminCommandsAsync(Platform.Discord).GetAwaiter().GetResult();

    public override Func<ulong, bool> IsAdmin =>
        userId => _discordConfiguration.AdminIdsArray.Contains(userId);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_isHandlerRegistered)
        {
            discordGatewayService.RegisterMessageCreatedHandler(HandleMessageCreatedAsync);
            _isHandlerRegistered = true;
            logger.LogInformation(
                "DiscordCommandService зарегистрировал обработчик MessageCreated"
            );
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override bool IsCommandAvailable(string commandName)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(commandName))
        {
            result = executor
                .IsCommandAvailableAsync(commandName, Platform.Discord)
                .GetAwaiter()
                .GetResult();
        }

        return result;
    }

    private async Task HandleMessageCreatedAsync(DiscordClient client, MessageCreatedEventArgs args)
    {
        if (args.Author is null || args.Author.IsBot)
        {
            return;
        }

        if (await discordPlayRequestService.TryHandleMessageAsync(client, args))
        {
            return;
        }

        var messageText = args.Message.Content ?? string.Empty;
        if (!StartsWithCommandPrefix(messageText))
        {
            return;
        }

        try
        {
            var commandParts = messageText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (commandParts.Length == 0)
            {
                return;
            }

            var commandName = TrimCommandPrefix(commandParts[0]);
            var input = commandParts.Length > 1 ? commandParts[1] : string.Empty;

            if (commandName.Equals("commands", StringComparison.OrdinalIgnoreCase))
            {
                var includeAdminCommands = IsUserAdmin(args.Author.Id);
                var commandsList = GetCommandsList(
                    args.Author.Id,
                    UserCommands,
                    AdminCommands,
                    includeAdminCommands
                );
                commandsList = AppendDiscordSpecificCommands(commandsList);
                await args.Channel.SendMessageAsync(ValidateResponse(commandsList));
            }
            else
            {
                var commandInfo = await commandService.GetCommandParametersAsync(commandName);
                if (commandInfo is not null)
                {
                    var requiredParams = commandInfo.Where(p => p.Required).ToArray();
                    var inputParts = string.IsNullOrWhiteSpace(input)
                        ? []
                        : BaseCommand.ParseParametersWithQuotes(input);

                    if (inputParts.Length >= requiredParams.Length)
                    {
                        var isAdminCommand = await commandService.IsAdminCommandAsync(commandName);
                        if (!isAdminCommand || IsUserAdmin(args.Author.Id))
                        {
                            var commandResult = await commandService.ExecuteCommandAsync(
                                commandName,
                                input,
                                Platform.Discord
                            );
                            await args.Channel.SendMessageAsync(ValidateResponse(commandResult));
                        }
                        else
                        {
                            await args.Channel.SendMessageAsync(
                                $"Команда '{commandName}' доступна только администраторам."
                            );
                        }
                    }
                    else
                    {
                        var missingParam = requiredParams[inputParts.Length];
                        var usage = string.Join(" ", requiredParams.Select(p => $"<{p.Name}>"));
                        await args.Channel.SendMessageAsync(
                            $"Не хватает параметра '{missingParam.Name}'. Использование: /{commandName} {usage}"
                        );
                    }
                }
                else
                {
                    await args.Channel.SendMessageAsync(
                        $"Команда '{commandName}' не найдена. Используйте /commands."
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обработки Discord команды");
            await args.Channel.SendMessageAsync("Ошибка выполнения команды.");
        }
    }

    private static string AppendDiscordSpecificCommands(string commandsList)
    {
        var result = commandsList;
        const string playCommandDescription =
            "/play - найти 10 треков на YouTube и прислать выбранный аудиофайл";

        if (!string.IsNullOrWhiteSpace(commandsList))
        {
            result = string.Concat(
                commandsList,
                Environment.NewLine,
                " | ",
                playCommandDescription
            );
        }
        else
        {
            result = string.Concat("Доступные команды: ", playCommandDescription);
        }

        return result;
    }
}
