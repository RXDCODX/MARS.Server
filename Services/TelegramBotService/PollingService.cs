using MARS.Server.Services.TelegramBotService.Abstract;

namespace MARS.Server.Services.TelegramBotService;

// Compose Polling and ReceiverService implementations
public class PollingService : PollingServiceBase<ReceiverService>
{
    public PollingService(IServiceProvider serviceProvider, ILogger<PollingService> logger)
        : base(serviceProvider, logger) { }
}
