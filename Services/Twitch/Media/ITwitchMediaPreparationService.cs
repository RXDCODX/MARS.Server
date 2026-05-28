using System.Threading;

namespace MARS.Server.Services.Twitch.Media;

public interface ITwitchMediaPreparationService
{
    Task<MediaInfo?> PrepareMediaAsync(
        Rewards._11_RandomMemReward.Service.Entity.MemeOrder memeOrder,
        string? displayName,
        CancellationToken cancellationToken = default,
        Func<string, Task>? onFileTranscoded = null
    );
}
