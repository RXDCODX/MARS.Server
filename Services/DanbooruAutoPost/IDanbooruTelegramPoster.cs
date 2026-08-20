using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.BooruShared.Entities;
using MARS.Server.Services.DanbooruAutoPost.Entities;

namespace MARS.Server.Services.DanbooruAutoPost;

public interface IDanbooruTelegramPoster
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
}
