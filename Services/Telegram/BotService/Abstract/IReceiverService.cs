namespace MARS.Server.Services.Telegram.BotService.Abstract;

/// <summary>
///     A marker interface for Update Receiver service
/// </summary>
public interface IReceiverService
{
    Task ReceiveAsync(AppDbContext appDbContext, CancellationToken stoppingToken);
}
