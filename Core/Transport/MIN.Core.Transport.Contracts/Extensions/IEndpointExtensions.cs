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
    /// Возвращает хост (ip или домен) из endpoint
    /// </summary>
    public static string GetHost(this IEndpoint endpoint)
    {
        var address = endpoint.GetAddress();
        var separatorIndex = address.LastIndexOf(':');
        return separatorIndex < 0 ? address : address[..separatorIndex];
    }

    /// <summary>
    /// Возвращает порт из endpoint
    /// </summary>
    public static int GetPort(this IEndpoint endpoint)
    {
        var address = endpoint.GetAddress();
        var separatorIndex = address.LastIndexOf(':');
        return separatorIndex < 0 ? 0 : int.Parse(address[(separatorIndex + 1)..]);
    }

    /// <summary>
    /// Указывают ли обе точки на один и тот же хост и порт
    /// </summary>
    public static bool SameTarget(this IEndpoint endpoint, IEndpoint other)
        => endpoint.Type == other.Type
           && string.Equals(endpoint.GetHost(), other.GetHost(), StringComparison.OrdinalIgnoreCase)
           && endpoint.GetPort() == other.GetPort();

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
            // домен (например, адрес VPS) — считаем внешним адресом
            return AddressOrigin.Wan;
        }

        var ipAddress = address.TryExtractMappedIPv4();

        if (ipAddress.IsPrivate())
        {
            return AddressOrigin.Lan;
        }

        return ipAddress.IsVpn() ? AddressOrigin.Vpn :
            // 3. Все остальное - WAN
            AddressOrigin.Wan;
    }
}
