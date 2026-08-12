using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services.BooruShared;

public interface IDeduplicationService
{
    Task<bool> IsAlreadyPostedAsync(
        string source,
        int imageId,
        ulong discordChannelId,
        CancellationToken cancellationToken = default
    );

    Task RecordPostAsync(
        string source,
        int imageId,
        ulong discordChannelId,
        CancellationToken cancellationToken = default
    );
}
