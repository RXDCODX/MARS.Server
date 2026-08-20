using System;
using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services.DanbooruAutoPost;

public interface IDanbooruTelegramPoster
{
    Task<OperationResult> PostAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<OperationResult> SchedulePostAsync(
        long chatId,
        byte[] fileBytes,
        string fileName,
        DateTime scheduleDate,
        CancellationToken cancellationToken
    );
}
