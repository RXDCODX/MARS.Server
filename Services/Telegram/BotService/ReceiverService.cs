using MARS.Server.Services.Telegram.BotService.Abstract;
using Telegram.Bot;

namespace MARS.Server.Services.Telegram.BotService;

// Compose Receiver and UpdateHandler implementation
public class ReceiverService(
    ITelegramBotClient botClient,
    UpdateHandler updateHandler,
    ILogger<ReceiverServiceBase<UpdateHandler>> logger
) : ReceiverServiceBase<UpdateHandler>(botClient, updateHandler, logger);
