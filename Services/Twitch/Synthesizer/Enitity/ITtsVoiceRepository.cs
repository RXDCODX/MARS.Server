namespace MARS.Server.Services.Twitch.Synthesizer.Enitity;

public interface ITtsVoiceRepository
{
    Task<List<string>> GetBlockedVoicesAsync(CancellationToken cancellationToken = default);
    Task<bool> AddBlockedVoiceAsync(
        string voiceName,
        CancellationToken cancellationToken = default
    );
    Task<bool> RemoveBlockedVoiceAsync(
        string voiceName,
        CancellationToken cancellationToken = default
    );
    Task EnsureDefaultBlockedVoicesAsync(CancellationToken cancellationToken = default);
}
