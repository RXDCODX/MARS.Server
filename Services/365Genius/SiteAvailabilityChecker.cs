using System.Net;
using MARS.Server.Exstensions;

namespace MARS.Server.Services._365Genius;

public sealed class SiteAvailabilityChecker(
    IDnsResolver dnsResolver,
    IHttpClientFactory httpClientFactory,
    ILogger<SiteAvailabilityChecker> logger
)
{
    public async Task<IPAddress[]> CheckDnsAsync(Uri site, CancellationToken cancellationToken)
    {
        IPAddress[]? addresses = null;

        try
        {
            addresses = await dnsResolver.GetHostAddressesAsync(site.Host, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogException(e);
            throw new HttpRequestException($"DNS resolution failed for {site.Host}", e);
        }

        if (addresses is null || addresses.Length == 0)
        {
            throw new HttpRequestException($"DNS did not resolve {site.Host}");
        }

        return addresses;
    }

    public async Task CheckPingPongAsync(Uri site, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(site, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Site {site} responded with {(int)response.StatusCode}"
            );
        }
    }

    public async Task CheckAllAsync(Uri site, CancellationToken cancellationToken)
    {
        await CheckDnsAsync(site, cancellationToken);
        await CheckPingPongAsync(site, cancellationToken);
    }
}
