using MARS.Server.Services.BooruAutoPost.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;

namespace MARS.Server.Services.BooruAutoPost;

public interface IBooruAutoPostService
{
    Task<OperationResult<List<BooruAutoPostConfigDto>>> GetAllAsync(
        BooruSource? source = null,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<BooruAutoPostConfigDto>> CreateAsync(
        BooruAutoPostCreateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<BooruAutoPostConfigDto>> UpdateAsync(
        BooruAutoPostUpdateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OperationResult<BooruAutoPostConfigDto>> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult> TriggerNowAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OperationResult<List<DiscordChannelOptionDto>>> GetDiscordChannelsAsync(
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<List<TelegramChannelOptionDto>>> GetTelegramChannelsAsync(
        CancellationToken cancellationToken = default
    );
}
