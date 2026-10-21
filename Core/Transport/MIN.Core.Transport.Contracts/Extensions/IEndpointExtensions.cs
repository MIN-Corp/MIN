using System.Net;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;

namespace MIN.Core.Transport.Contracts.Extensions;

/// <summary>
/// Расширения для <see cref="IEndpoint"/>
/// </summary>
public static class IEndpointExtensions
{
    /// <summary>
    /// Возвращает true, если указанный IP-адрес зарезервирован для частных сетей
    /// </summary>
    /// <remarks>
    ///     See reserved <a href="https://en.wikipedia.org/wiki/Reserved_IP_addresses#IPv4">IPv4</a> and
    ///     <a href="https://en.wikipedia.org/wiki/Reserved_IP_addresses#IPv6">IPv6</a> address ranges.
    /// </remarks>
    public static AddressOrigin AssumeOriginOutOfAddress(this IEndpoint endpoint)
    {
        if (!IPAddress.TryParse(endpoint.GetAddress().Split(':')[0], out var address))
        {
            throw new ArgumentException("Can't parse IpAddress out of endpoint");
        }

        var ipAddress = address.TryExtractMappedIPv4();

        if (ipAddress.IsPrivate())
        {
            return AddressOrigin.Lan;
        }

        if (ipAddress.IsVpn())
        {
            return AddressOrigin.Vpn;
        }

        // 3. Все остальное - WAN
        return AddressOrigin.Wan;
    }
}
