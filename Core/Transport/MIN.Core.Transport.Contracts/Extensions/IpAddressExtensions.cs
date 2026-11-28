using System.Net;
using System.Net.Sockets;

namespace MIN.Core.Transport.Contracts.Extensions;

/// <summary>
/// Расширения для <see cref="IPAddress"/>
/// </summary>
public static class IpAddressExtensions
{
    private readonly static Cidr[] vpnNetworks =
    [
        "25.0.0.0/8",
        "5.0.0.0/8",

        "255.255.255.0/24", // Некоторые версии Radmin используют 255.255.255.0/24
        "26.0.0.0/8",       // Диапазон Radmin VPN

        "192.168.192.0/20", // ZeroTier по умолчанию
        "25.0.0.0/8",       // ZeroTier также использует 25.x.x.x

        "100.64.0.0/10",    // Tailscale использует CGNAT диапазон

        "10.0.0.0/8",       // Частный, но если не в LAN - может быть VPN
        "172.16.0.0/12",    // Частный, но если не в LAN - может быть VPN
        "192.168.0.0/16",   // Частный, но если не в LAN - может быть VPN

        "100.64.0.0/10",
    ];

    /// <summary>
    /// Возвращает true, если указанный IP-адрес зарезервирован для частных сетей
    /// </summary>
    /// <remarks>
    ///     See reserved <a href="https://en.wikipedia.org/wiki/Reserved_IP_addresses#IPv4">IPv4</a> and
    ///     <a href="https://en.wikipedia.org/wiki/Reserved_IP_addresses#IPv6">IPv6</a> address ranges.
    /// </remarks>
    public static bool IsPrivate(this IPAddress address)
    {
        address = address.TryExtractMappedIPv4();
        Cidr[] privateSubnets = address.AddressFamily == AddressFamily.InterNetwork ? Cidr.PrivateIPv4Networks : Cidr.PrivateIPv6Networks;
        var addressBytes = address.GetAddressBytes();
        foreach (Cidr privateSubnet in privateSubnets)
        {
            if (privateSubnet.ContainsHost(addressBytes))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Проверяет, принадлежит ли IP-адрес VPN-сетям
    /// </summary>
    public static bool IsVpn(this IPAddress address)
    {
        address = address.TryExtractMappedIPv4();

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var addressBytes = address.GetAddressBytes();

        foreach (var vpnSubnet in vpnNetworks)
        {
            if (vpnSubnet.ContainsHost(addressBytes))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Пытается получить IPv4-адрес, если он был сопоставлен с другим IP-пространством, например, IPv6.
    /// </summary>
    /// <remarks>
    ///     IPv6 address space supports the entire IPv4 address space using a reserved subspace:
    ///     <code>::ffff:0:0/96</code>
    ///     <b>First address:</b>
    ///     <code>
    ///      ::ffff:0.0.0.0
    ///      ::ffff:0:0
    ///      </code>
    ///     <b>Last address:</b>
    ///     <code>
    ///      ::ffff:255.255.255.255
    ///      ::ffff:ffff:ffff
    ///      </code>
    /// </remarks>
    public static IPAddress TryExtractMappedIPv4(this IPAddress address)
        => address == null
            ? throw new ArgumentNullException(nameof(address))
            : address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    /// <summary>
    /// Извлечь канонический IP-адрес из строки удалённой точки подключения
    /// </summary>
    /// <param name="endPoint">Строка вида "1.2.3.4:56789" или "[::1]:8080"</param>
    /// <param name="ip">Канонический IP-адрес (IPv4-mapped IPv6 разворачивается в IPv4)</param>
    /// <returns>True - если адрес удалось разобрать</returns>
    public static bool TryGetCanonicalIp(this string? endPoint, out string ip)
    {
        ip = string.Empty;

        if (string.IsNullOrWhiteSpace(endPoint))
        {
            return false;
        }

        try
        {
            var address = IPEndPoint.Parse(endPoint).Address.TryExtractMappedIPv4();
            ip = address.ToString();
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <remarks>
    /// See <a href="https://en.wikipedia.org/wiki/Classless_Inter-Domain_Routing">CIDR on Wikipedia</a>
    /// </remarks>
    private readonly record struct Cidr(byte[] NotationBytes, byte NetworkMaskBitSize)
    {
        /// <remarks>
        /// See <a href="https://en.wikipedia.org/wiki/Reserved_IP_addresses#IPv4">all reserved IPv4 address ranges</a>.
        /// </remarks>
        public readonly static Cidr[] PrivateIPv4Networks =
        [
            "0.0.0.0/8", // "This" network
            "10.0.0.0/8",
            "127.0.0.0/8", // Loopback
            "172.16.0.0/12",
            "192.0.0.0/24 ",
            "192.168.0.0/16",
            "198.18.0.0/15"
        ];

        /// <remarks>
        /// See <a href="https://en.wikipedia.org/wiki/Reserved_IP_addresses#IPv6">all reserved IPv6 address ranges</a>.
        /// </remarks>
        public readonly static Cidr[] PrivateIPv6Networks =
        [
            "::/128", // Unspecified address
            "::1/128", // Loopback
            "fc00::/7", // Unique local address
            "fe80::/10", // Link-local address
            "64:ff9b:1::/48" // Local-use IPv4/IPv6 translation
        ];

        public static implicit operator Cidr(string notation)
        {
            if (notation.LastIndexOf('/') is not (var slashIndex and >= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(notation));
            }
            if (!byte.TryParse(notation.Substring(slashIndex + 1), out byte networkMaskBitSize))
            {
                throw new ArgumentOutOfRangeException(nameof(notation), "CIDR network mask bit size is not a valid byte value");
            }
            byte[] notationBytes = IPAddress.Parse(notation.Substring(0, slashIndex)).GetAddressBytes();
            if (networkMaskBitSize > notationBytes.Length * 8)
            {
                throw new ArgumentOutOfRangeException(nameof(notation), "CIDR network mask bit size must not be more than total length of the CIDR notation");
            }
            return new Cidr(notationBytes, networkMaskBitSize);
        }

        /// <summary>
        /// Returns true if the subnet defined by the CIDR notation could contain the host address.
        /// </summary>
        public bool ContainsHost(byte[] hostAddress)
        {
            // Compare the address with a CIDR mask, depending on the bit size.
            var hostAddressBitSize = hostAddress.Length * 8;
            switch (hostAddressBitSize)
            {
                case 32:
                    {
                        var hostAddressNum = BitConverter.ToInt32(hostAddress, 0);
                        var notationNum = BitConverter.ToInt32(NotationBytes, 0);
                        var networkBitMask = IPAddress.HostToNetworkOrder(-1 << (hostAddressBitSize - NetworkMaskBitSize));
                        return (hostAddressNum & networkBitMask) == (notationNum & networkBitMask);
                    }
                default:
                    {
                        var fullBytes = NetworkMaskBitSize / 8;
                        var remainingBits = NetworkMaskBitSize % 8;

                        // 1. Сравниваем целые байты, которые полностью в маске
                        if (!hostAddress.AsSpan(0, fullBytes).SequenceEqual(NotationBytes.AsSpan(0, fullBytes)))
                        {
                            return false;
                        }

                        // 2. Если есть неполный байт — маскируем его и сравниваем
                        if (remainingBits > 0)
                        {
                            var mask = (byte)(0xFF << (8 - remainingBits)); // старшие remainingBits бит = 1
                            if ((hostAddress[fullBytes] & mask) != (NotationBytes[fullBytes] & mask))
                            {
                                return false;
                            }
                        }

                        return true;
                    }
            }
        }
    }
}
