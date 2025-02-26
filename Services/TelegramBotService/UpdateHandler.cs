using System.Diagnostics;
using System.Reflection;
using Hangfire;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem;
using MARS.Server.Services.TelegramBotService.Commands.Attribute;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace MARS.Server.Services.TelegramBotService;

public class UpdateHandler : IUpdateHandler
{
    public delegate Task TelegramUpdateDelegate(Update update);
    public event TelegramUpdateDelegate TelegramUpdate = (update) => Task.CompletedTask;

    private readonly ITelegramBotClient _botClient;
    private readonly Commands.Commands _commands;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly TelegramConfiguration _options;

    public UpdateHandler(
        ITelegramBotClient botClient,
        ILogger<UpdateHandler> logger,
        Commands.Commands commands,
        IOptions<TelegramConfiguration> options,
        PyroAlertsHandler pyroAlertsHandler,
        RandomMemHandler randomMemHandler,
        IHostApplicationLifetime applicationLifetime
    )
    {
        _botClient = botClient;
        _logger = logger;
        _commands = commands;
        _options = options.Value;

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            TelegramUpdate += pyroAlertsHandler.HandAlert;
            TelegramUpdate += randomMemHandler.HandMessage;
        });
    }

    public async Task HandleUpdateAsync(
        ITelegramBotClient _,
        Update update,
        CancellationToken cancellationToken
    )
    {
        var id = BackgroundJob.Enqueue(() => ResendMessage(update));

        var handler = update switch
        {
            //{ ChannelPost: {} channelPost } => BotOnChannelPost(channelPost, cancellationToken),
            { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
            { InlineQuery: { } inlineQuery } => BotOnInlineQueryReceived(
                inlineQuery,
                cancellationToken
            ),
            _ => UnknownUpdateHandlerAsync(update, cancellationToken),
        };

        await handler;
        await TelegramUpdate.Invoke(update);
    }

    public Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken
    )
    {
        _logger.LogException(exception);
        return Task.CompletedTask;
    }

    public async Task HandlePollingErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString(),
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", errorMessage);

        // Cooldown in case of network connection error
        if (exception is RequestException)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    public async Task ResendMessage(Update update)
    {
        foreach (var id in _options.AdminIdsArray)
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    var messageId = update.Message!.MessageId;
                    var chatId = update.Message.Chat.Id;

                    if (update.Message.HasProtectedContent != true)
                    {
                        await _botClient.ForwardMessage(id, chatId, messageId);
                    }

                    break;
                case UpdateType.ChannelPost:
                    messageId = update.ChannelPost!.MessageId;
                    chatId = update.ChannelPost.Chat.Id;

                    if (update.ChannelPost.HasProtectedContent != true)
                    {
                        await _botClient.ForwardMessage(id, chatId, messageId);
                    }

                    //if (_environment.IsDevelopment())
                    //    _logger.LogCritical(update.ChannelPost.Text);
                    break;
            }
        }
    }

    private async Task BotOnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Receive message type: {MessageType}", message.Type);

        if (
            message.Type != MessageType.Text
            || message.Text is not { } messageText
            || !messageText.StartsWith("/")
        )
        {
            return;
        }

        Task<Message>? action;

        try
        {
            var command = messageText.Split(' ')[0];
            var methodName = GetMethodName(command);

            var method = _commands
                .GetType()
                .GetMethod(
                    methodName,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance
                );
            if (method == null)
            {
                action = ErrorCommand(_botClient, message, cancellationToken);
            }
            else
            {
                // Проверка, является ли метод административным
                var isAdminMethod = method.GetCustomAttribute<AdminAttribute>() != null;
                var isIgnore = method.GetCustomAttribute<IgnoreAttribute>() != null;
                var isAdminUser = _options.AdminIdsArray.Any(e => e == message.Chat.Id);

                if (isIgnore || (isAdminMethod && !isAdminUser))
                {
                    action = ErrorCommand(_botClient, message, cancellationToken);
                }
                else
                {
                    var parameters = new object[] { _botClient, message, cancellationToken };
                    if (methodName == "OnCommandsCommandReceived" && isAdminUser)
                    {
                        parameters = new object[] { _botClient, message, cancellationToken, true };
                    }

                    action = (Task<Message>?)method.Invoke(_commands, parameters);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command");
            action = ErrorCommand(_botClient, message, cancellationToken);
        }

        if (action != null)
        {
            var sentMessage = await action.ConfigureAwait(false);
            _logger.LogInformation(
                "The message was sent with id: {SentMessageId}",
                sentMessage.MessageId
            );
        }
    }

    private string GetMethodName(string command)
    {
        // Преобразуем команду в имя метода, например, "/genshin" -> "OnGenshinCommandReceived"
        return "On"
            + command.Substring(1).First().ToString().ToUpper()
            + command.Substring(2)
            + "CommandReceived";
    }

    private Task<Message>? ErrorCommand(
        ITelegramBotClient client,
        Message message,
        CancellationToken cancellationToken
    )
    {
        return client.SendMessage(
            message.Chat.Id,
            Commands.Commands.Template,
            cancellationToken: cancellationToken
        );
    }

    #region Inline Mode

    private async Task BotOnInlineQueryReceived(
        InlineQuery inlineQuery,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Received inline query from: {InlineQueryFromId}",
            inlineQuery.From.Id
        );

        InlineQueryResult[] results =
        {
            // displayed result
            new InlineQueryResultArticle("1", "TgBots", new InputTextMessageContent("hello")),
        };

        await _botClient.AnswerInlineQuery(
            inlineQuery.Id,
            results,
            0,
            true,
            cancellationToken: cancellationToken
        );
    }

    #endregion

#pragma warning disable IDE0060 // Remove unused parameter
    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }
}
