using MARS.Server.Services.TelegramBotService.Abstract;

namespace MARS.Server.Services.TelegramBotService;

// Compose Receiver and UpdateHandler implementation
public class ReceiverService(
    ITelegramBotClient botClient,
    UpdateHandler updateHandler,
    ILogger<ReceiverServiceBase<UpdateHandler>> logger
) : ReceiverServiceBase<UpdateHandler>(botClient, updateHandler, logger);
