using Hangfire;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace MARS.Server.Services.TelegramBotService;

public class UpdateHandler : IUpdateHandler
{
    public delegate void TelegramUpdateDelegate(ITelegramBotClient client, Update update);
    public event TelegramUpdateDelegate TelegramUpdate = (client, update) => { };

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

    public Task HandleUpdateAsync(
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

        var id2 = BackgroundJob.ContinueJobWith(id, () => handler);
        BackgroundJob.ContinueJobWith(id2, () => TelegramUpdate.Invoke(_, update));

        return Task.CompletedTask;
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

        if (message.Type == MessageType.Text)
        {
            if (message.Text is not { } messageText)
            {
                return;
            }

            if (!messageText.StartsWith("/"))
            {
                return;
            }

            Task<Message> action;

            if (_options.AdminIdsArray.Any(e => e == message.Chat.Id))
            {
                action = messageText.Split(' ')[0] switch
                {
                    "/help" => _commands.OnHelpCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/honkai" => _commands.OnHonkaiCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/links" => _commands.OnLinksCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/start" => _commands.OnStartCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/whitelist" => _commands.OnWhiteListCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/honkaiusers" => _commands.OnHonkaiUsersCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/directory" => _commands.OnDirectoryCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/raidhelp" => _commands.OnRaidHelpCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/streamup" => _commands.OnStreamupCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/catisa" => _commands.OnCatisaCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/commands" => _commands.OnUsageCommandReceived(
                        _botClient,
                        message,
                        cancellationToken,
                        true
                    ),
                    "/twitchevents" => _commands.OnTwitchEventsCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/tanya" => _commands.OnTanyaCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/ttsvolume" => _commands.OnTTSVolumeCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/zonezero" => _commands.OnZoneZeroCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/twitchsubrec" => _commands.OnTwitchSubRecCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/byebye" => _commands.OnByeByeCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/fumo" => _commands.OnFumoCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/hellovideo" => _commands.OnHelloVideoCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/joinedtwitchchannels" => _commands.OnJoinedTwitchChannelsCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/genshin" => _commands.OnGenshinCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    _ => ErrorCommand(_botClient, message, cancellationToken),
                };
            }
            else
            {
                action = messageText.Split(' ')[0] switch
                {
                    "/help" => _commands.OnHelpCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/honkai" => _commands.OnHonkaiCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/links" => _commands.OnLinksCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/start" => _commands.OnStartCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/whitelist" => _commands.OnWhiteListCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/honkaiusers" => _commands.OnHonkaiUsersCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/raidhelp" => _commands.OnRaidHelpCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/streamup" => _commands.OnStreamupCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/commands" => _commands.OnUsageCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/zonezero" => _commands.OnZoneZeroCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/byebye" => _commands.OnByeByeCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    "/genshin" => _commands.OnGenshinCommandReceived(
                        _botClient,
                        message,
                        cancellationToken
                    ),
                    _ => ErrorCommand(_botClient, message, cancellationToken),
                };
            }

            static Task<Message> ErrorCommand(
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

            var sentMessage = await action.ConfigureAwait(false);
            _logger.LogInformation(
                "The message was sent with id: {SentMessageId}",
                sentMessage.MessageId
            );
        }
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
