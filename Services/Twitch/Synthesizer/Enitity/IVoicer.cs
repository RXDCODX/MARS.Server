namespace MARS.Server.Services.Twitch.Synthesizer.Enitity;

public interface IVoicer
{
    bool IsActive { get; set; }
    int GetVolume();
    void ChangeVolume(int volume);
    Task Sound(MessageToSynthezid message);
    Task Stop();
    Task Block();
    Task Unblock();
    Task RefreshBlockedVoicesAsync(CancellationToken cancellationToken = default);
    Task ResetVoiceAsync(string name, CancellationToken cancellationToken = default);
    Task ResetAllVoicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetLinkedVoicesAsync(
        CancellationToken cancellationToken = default
    );
    Task<List<string>> GetInstalledVoicesAsync(CancellationToken cancellationToken = default);
}
