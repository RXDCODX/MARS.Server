using MARS.Server.Services.CommandExecutor;
using MARS.Server.Services.CommandExecutor.Adapters;
using MARS.Server.Services.CommandExecutor.Entitys;
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
    private readonly ICommandService _commandService;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly TelegramConfiguration _options;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public UpdateHandler(
        ITelegramBotClient botClient,
        ILogger<UpdateHandler> logger,
        ICommandService commandService,
        IOptions<TelegramConfiguration> options,
        PyroAlertsHandler pyroAlertsHandler,
        RandomMemHandler randomMemHandler,
        IHostApplicationLifetime applicationLifetime,
        Tekken8FrameData frameData,
        IDbContextFactory<AppDbContext> dbContextFactory,
        TelegramCommandService telegramCommandService
    )
    {
        _botClient = botClient;
        _logger = logger;
        _commandService = commandService;
        _dbContextFactory = dbContextFactory;
        _options = options.Value;

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            TelegramUpdate += pyroAlertsHandler.HandAlert;
            TelegramUpdate += randomMemHandler.HandMessage;
            TelegramUpdate += frameData.HandAlert;
            TelegramUpdate += telegramCommandService.HandMessage;
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
        [
            // displayed result
            new InlineQueryResultArticle("1", "TgBots", new InputTextMessageContent("hello")),
        ];

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
