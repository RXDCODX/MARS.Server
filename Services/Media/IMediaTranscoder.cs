using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services.Media;

public interface IMediaTranscoder
{
    /// <summary>
    /// Ensure the file at <paramref name="sourceFullPath"/> is playable (meets bitrate/frame requirements).
    /// Returns the full path to the playable file (may be the same as source).
    /// </summary>
    Task<string> EnsurePlayableAsync(string sourceFullPath, CancellationToken cancellationToken = default);
}
