using MARS.Server.Services.TelegramBotService.Abstract;

namespace MARS.Server.Services.TelegramBotService;

// Compose Receiver and UpdateHandler implementation
public class ReceiverService : ReceiverServiceBase<UpdateHandler>
{
    public ReceiverService(
        ITelegramBotClient botClient,
        UpdateHandler updateHandler,
        ILogger<ReceiverServiceBase<UpdateHandler>> logger
    )
        : base(botClient, updateHandler, logger) { }
}
