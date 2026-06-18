using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services.Twitch.Synthesizer;

public interface ITtsMessageFilterService
{
    bool IsFilterEnabled { get; set; }

    OperationResult<string> FilterMessage(string message, string? userId = null);

    Task LoadStateAsync(CancellationToken cancellationToken = default);
}
