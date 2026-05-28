using MARS.Server.Services.Telegram.BotService.Abstract;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Telegram.BotService;

// Compose Polling and ReceiverService implementations
public class PollingService(
    IServiceProvider serviceProvider,
    ILogger<PollingService> logger,
    IDbContextFactory<AppDbContext> factory
) : PollingServiceBase<ReceiverService>(serviceProvider, logger, factory);
