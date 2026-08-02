using System.Net;

namespace MARS.Server.Services._365Genius;

public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(
        string hostNameOrAddress,
        CancellationToken cancellationToken
    );
}
