using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
