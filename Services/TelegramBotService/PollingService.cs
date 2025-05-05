using MARS.Server.Services.TelegramBotService.Abstract;

namespace MARS.Server.Services.TelegramBotService;

// Compose Polling and ReceiverService implementations
public class PollingService(
    IServiceProvider serviceProvider,
    ILogger<PollingService> logger,
    IDbContextFactory<AppDbContext> factory
) : PollingServiceBase<ReceiverService>(serviceProvider, logger, factory);
