using MARS.Server.Services.NSFWBooru.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;

namespace MARS.Server.Services.NSFWBooru;

public interface INSFWBooruAutoPostService
{
    Task<OperationResult<List<NSFWBooruAutoPostConfigDto>>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<NSFWBooruAutoPostConfigDto>> CreateAsync(
        NSFWBooruAutoPostCreateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<NSFWBooruAutoPostConfigDto>> UpdateAsync(
        NSFWBooruAutoPostUpdateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OperationResult<NSFWBooruAutoPostConfigDto>> SetEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult> TriggerNowAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OperationResult<List<DiscordChannelOptionDto>>> GetDiscordChannelsAsync(
        CancellationToken cancellationToken = default
    );
}
