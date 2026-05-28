using System.Threading;

namespace MARS.Server.Services.Media;

public interface IMediaInspector
{
    Task<(long? BitrateKbps, double? AverageFrameRate, double? RawFrameRate)> ProbeAsync(string filePath, CancellationToken cancellationToken = default);
}
