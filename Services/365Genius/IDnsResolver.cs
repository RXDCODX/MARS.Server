using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services._365Genius;

public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(
        string hostNameOrAddress,
        CancellationToken cancellationToken
    );
}
