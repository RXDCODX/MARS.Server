using Telegram.Bot.Polling;

namespace MARS.Server.Services.Telegram.BotService.Abstract;

/// <summary>
///     An abstract class to compose Receiver Service and Update Handler classes
/// </summary>
/// <typeparam name="TUpdateHandler">Update Handler to use in Update Receiver</typeparam>
public abstract class ReceiverServiceBase<TUpdateHandler> : IReceiverService
    where TUpdateHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<ReceiverServiceBase<TUpdateHandler>> _logger;
    private readonly IUpdateHandler _updateHandler;

    internal ReceiverServiceBase(
        ITelegramBotClient botClient,
        TUpdateHandler updateHandler,
        ILogger<ReceiverServiceBase<TUpdateHandler>> logger
    )
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _logger = logger;
    }

    /// <summary>
    ///     Start to service Updates with provided Update Handler class
    /// </summary>
    /// <param name="context"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    public async Task ReceiveAsync(AppDbContext context, CancellationToken stoppingToken)
    {
        // ToDo: we can inject ReceiverOptions through IOptions container

        var offset = await GetOffset(context, stoppingToken);
        var isOffsetRequired = offset is not null;

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [],
            DropPendingUpdates = !isOffsetRequired,
            Offset = offset?.Offset,
        };

        var me = await _botClient.GetMe(stoppingToken);
        _logger.LogInformation(
            "Start receiving updates for {BotName}",
            me.Username ?? "My Awesome Bot"
        );

        // Start receiving updates
        await _botClient.ReceiveAsync(_updateHandler, receiverOptions, stoppingToken);
    }

    private static async Task<TelegramUpdateReceiverOffset?> GetOffset(
        AppDbContext context,
        CancellationToken token
    )
    {
        var offset = await context.TelegramUpdateReceiverOffset.SingleOrDefaultAsync(
            cancellationToken: token
        );
        return offset;
    }
}
