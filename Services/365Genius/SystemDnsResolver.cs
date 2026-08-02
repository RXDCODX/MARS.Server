using System.Net;

namespace MARS.Server.Services._365Genius;

public sealed class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> GetHostAddressesAsync(
        string hostNameOrAddress,
        CancellationToken cancellationToken
    )
    {
        return Dns.GetHostAddressesAsync(hostNameOrAddress, cancellationToken);
    }
}
