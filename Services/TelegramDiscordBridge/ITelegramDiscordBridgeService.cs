using MARS.Server.Services.TelegramDiscordBridge.Entitys;

namespace MARS.Server.Services.TelegramDiscordBridge;

public interface ITelegramDiscordBridgeService
{
    Task<OperationResult<List<TelegramDiscordBindingDto>>> GetBindingsAsync(
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<TelegramDiscordBindingDto>> AddBindingAsync(
        TelegramDiscordBindingCreateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult> DeleteBindingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OperationResult<TelegramDiscordBindingDto>> SetBindingEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<List<TelegramDiscordChannelStateDto>>> GetStatesAsync(
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<List<TelegramChannelOptionDto>>> GetTelegramChannelsAsync(
        CancellationToken cancellationToken = default
    );

    Task<OperationResult<List<DiscordChannelOptionDto>>> GetDiscordChannelsAsync(
        CancellationToken cancellationToken = default
    );
}
