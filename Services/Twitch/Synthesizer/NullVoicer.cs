using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class NullVoicer(ILogger<IVoicer> logger) : IVoicer
{
    public bool IsActive { get; set; }

    public int GetVolume()
    {
        logger.LogWarning("Changing volume is not supported on this platform.");
        return 0;
    }

    public void ChangeVolume(int volume)
    {
        logger.LogWarning("Changing volume is not supported on this platform.");
    }

    public Task Stop()
    {
        return Task.CompletedTask;
    }

    public Task Block()
    {
        return Task.CompletedTask;
    }

    public Task Unblock()
    {
        return Task.CompletedTask;
    }

    public Task RefreshBlockedVoicesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Speech synthesis is not supported on this platform.");
        return Task.CompletedTask;
    }

    public Task ResetVoiceAsync(string name, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Speech synthesis is not supported on this platform.");
        return Task.CompletedTask;
    }

    public Task ResetAllVoicesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Speech synthesis is not supported on this platform.");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, string>> GetLinkedVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        logger.LogWarning("Speech synthesis is not supported on this platform.");
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    public Task<List<string>> GetInstalledVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        logger.LogWarning("Speech synthesis is not supported on this platform.");
        return Task.FromResult(new List<string>());
    }

    public Task Sound(MessageToSynthezid message)
    {
        return Task.CompletedTask;
    }
}
