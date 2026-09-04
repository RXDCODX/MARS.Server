using MARS.Server.Services.BooruAutoPost.Entities;
using MARS.Server.Services.BooruShared.Entities;

namespace MARS.Server.Services.BooruAutoPost;

public interface IBooruTelegramPoster
{
    Task<OperationResult> PostAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        string message,
        TelegramParseMode parseMode,
        CancellationToken cancellationToken
    );

    Task<OperationResult> SchedulePostAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        string message,
        TelegramParseMode parseMode,
        DateTime scheduleDate,
        CancellationToken cancellationToken
    );

    Task<OperationResult<List<TelegramScheduledMessageInfo>>> GetScheduledMessagesAsync(
        long chatId,
        CancellationToken cancellationToken
    );

    Task<OperationResult> DeleteScheduledMessagesAsync(
        long chatId,
        IReadOnlyCollection<int> messageIds,
        CancellationToken cancellationToken
    );
}
