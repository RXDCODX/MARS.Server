using MARS.Server.Services.DanbooruAutoPost.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;

namespace MARS.Server.Services.DanbooruAutoPost;

public interface IDanbooruAutoPostService
{
    Task<OperationResult<List<DanbooruAutoPostConfigDto>>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<DanbooruAutoPostConfigDto>> CreateAsync(
        DanbooruAutoPostCreateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<DanbooruAutoPostConfigDto>> UpdateAsync(
        DanbooruAutoPostUpdateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OperationResult<DanbooruAutoPostConfigDto>> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult> TriggerNowAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OperationResult<List<DiscordChannelOptionDto>>> GetDiscordChannelsAsync(
        CancellationToken cancellationToken = default
    );
}
