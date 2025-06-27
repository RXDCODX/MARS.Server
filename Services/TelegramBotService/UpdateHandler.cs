using System.Reflection;
using MARS.Server.Services.Framedata;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace MARS.Server.Services.TelegramBotService;

public class UpdateHandler : IUpdateHandler
{
    public delegate Task TelegramUpdateDelegate(ITelegramBotClient client, Update update);

    private readonly ITelegramBotClient _botClient;
    private readonly Commands.Commands _commands;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly TelegramConfiguration _options;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public UpdateHandler(
        ITelegramBotClient botClient,
        ILogger<UpdateHandler> logger,
        Commands.Commands commands,
        IOptions<TelegramConfiguration> options,
        PyroAlertsHandler pyroAlertsHandler,
        RandomMemHandler randomMemHandler,
        IHostApplicationLifetime applicationLifetime,
        Tekken8FrameData frameData,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _botClient = botClient;
        _logger = logger;
        _commands = commands;
        _dbContextFactory = dbContextFactory;
        _options = options.Value;

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            TelegramUpdate += pyroAlertsHandler.HandAlert;
            TelegramUpdate += randomMemHandler.HandMessage;
            TelegramUpdate += frameData.HandAlert;
        });
    }

    public async Task HandleUpdateAsync(
        ITelegramBotClient _,
        Update update,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await UpdateOffset(update.Id, cancellationToken);

            ResendMessage(update);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }

        Task handler = update switch
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
        await TelegramUpdate.Invoke(_, update);
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

    public event TelegramUpdateDelegate TelegramUpdate = (client, update) => Task.CompletedTask;

    private async void ResendMessage(Update update)
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
            || !messageText.StartsWith('/')
        )
        {
            return;
        }

        Task<Message>? action;

        try
        {
            var command = messageText.Split(' ')[0];
            var methodName = GetMethodName(command);
            var methods = _commands
                .GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            var method = methods.FirstOrDefault(e =>
                e.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)
            );
            if (method == null)
            {
                var methodWithAliases = methods.Where(e =>
                    e.GetCustomAttribute<AliasAttribute>() != null
                );
                var commandWithoutSlash = command.Substring(1);
                method = methodWithAliases.FirstOrDefault(
                    e =>
                    {
                        var aliasAttr = e?.GetCustomAttribute<AliasAttribute>();
                        if (aliasAttr?.MethodAliases.Contains(commandWithoutSlash) == true)
                        {
                            return true;
                        }

                        return false;
                    },
                    null
                );
            }

            if (method != null)
            {
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
                    if (methodName == "OnCommandsCommandReceived")
                    {
                        if (isAdminUser)
                        {
                            parameters = [_botClient, message, cancellationToken, true];
                        }
                        else
                        {
                            parameters = [_botClient, message, cancellationToken, false];
                        }
                    }

                    action = (Task<Message>?)method.Invoke(_commands, parameters);
                }
            }
            else
            {
                action = ErrorCommand(_botClient, message, cancellationToken);
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

    private string GetMethodName(string command)
    {
        return string.Concat(
            "On",
            command.Substring(1).First().ToString().ToUpper(),
            command.AsSpan(2),
            "CommandReceived"
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

    private async Task UpdateOffset(int updateId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var offset = await dbContext.TelegramUpdateReceiverOffset.SingleOrDefaultAsync(
            cancellationToken: cancellationToken
        );

        if (offset is not null)
        {
            if (updateId != offset.Offset + 1)
            {
                offset.Offset = updateId;
            }
            else
            {
                offset.Offset += 1;
            }
        }
        else
        {
            var obset = new TelegramUpdateReceiverOffset()
            {
                Offset = updateId,
                Id = Guid.NewGuid(),
            };

            await dbContext.TelegramUpdateReceiverOffset.AddAsync(obset, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
