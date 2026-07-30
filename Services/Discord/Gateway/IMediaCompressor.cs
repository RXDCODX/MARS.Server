using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services.Discord.Gateway;

public interface IMediaCompressor
{
    Task<Stream?> CompressImageAsync(
        Stream source,
        string fileName,
        long maxSize,
        CancellationToken ct
    );

    Task<IReadOnlyList<(Stream Stream, string FileName)>?> CompressVideoAsync(
        Stream source,
        string fileName,
        long maxSize,
        CancellationToken ct
    );

    Task<Stream?> CompressAudioAsync(
        Stream source,
        string fileName,
        long maxSize,
        CancellationToken ct
    );
}
