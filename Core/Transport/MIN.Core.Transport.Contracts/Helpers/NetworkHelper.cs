using System.Collections.Immutable;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Extensions;
using MIN.Core.Transport.Contracts.Models;

namespace MIN.Core.Transport.Contracts.Helpers;

/// <summary>
/// Помошник в сети
/// </summary>
public static partial class NetworkHelper
{
    private static ImmutableArray<string> PublicIpResolverServiceUrls =>
    [
        "https://ipv4.icanhazip.com/",
        "https://checkip.amazonaws.com/",
        "https://api.ipify.org/",
        "https://api4.my-ip.io/ip",
        "https://ifconfig.me/",
        "https://showmyip.com/",
    ];

    private static ImmutableArray<string> SupportedVpnNames =>
    [
        "Hamachi",
        "Radmin VPN",
    ];

    private static IPAddress? wanIpCache;
    private static IPAddress? lanIpCache;
    private static long lastSeenPacketChange = -1;
    private static long lastBytesSendOrReceived = -1;
    private readonly static object connectivityLock = new();
    private readonly static object wanIpLock = new();
    private readonly static object lanIpLock = new();

    private static bool? hasInternet;

    [GeneratedRegex(@"(?:[0-2]??[0-9]{1,2}\.){3}[0-2]??[0-9]+")]
    private static partial Regex IpAddressRegex();

    /// <summary>
    ///     Gets the network interfaces used for going onto the internet.
    ///     This is done by filtering for "Ethernet" and "Wi-Fi" network interfaces where "Ethernet" is returned earlier.
    /// </summary>
    /// <returns>Network interfaces used to go onto the internet.</returns>
    public static IEnumerable<NetworkInterface> GetInternetInterfaces() =>
        NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus is OperationalStatus.Up
                            && n.NetworkInterfaceType is not (NetworkInterfaceType.Tunnel or NetworkInterfaceType.Loopback)
                            && n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet
                            && n.GetIPProperties().GatewayAddresses.Count != 0)
                        .OrderBy(n => n.NetworkInterfaceType is NetworkInterfaceType.Ethernet ? 1 : 0)
                        .ThenBy(n => n.Name);

    /// <summary>
    ///     Tries to get the most convenient LAN-usable IP address.
    /// </summary>
    /// <remarks>
    ///     If IPv6 is returned, no IPv4 is available and this IP might also be usable for WAN.
    /// </remarks>
    public static IPAddress? GetLanUsableIp()
    {
        lock (lanIpLock)
        {
            if (lanIpCache != null)
            {
                return lanIpCache;
            }
        }

        IPAddress? ipAddress = GetLocalMachineAttachedIpAddresses().OrderBy(a => a.Address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).FirstOrDefault()?.Address;
        if (ipAddress == null)
        {
            return null;
        }
        lock (lanIpLock)
        {
            return lanIpCache = ipAddress;
        }
    }

    /// <summary>
    /// Получить публичный IP адрес
    /// </summary>
    public static async Task<IPAddress?> GetWanIpAsync(CancellationToken cancellationToken)
    {
        lock (wanIpLock)
        {
            if (wanIpCache != null)
            {
                return wanIpCache;
            }
        }

        var ip = await PortForwardingHelper.GetExternalIpAsync(cancellationToken);
        var canceled = false;

        if (ip == null || ip.IsPrivate())
        {
            Regex regex = IpAddressRegex();
            using HttpClient client = new();
            foreach (var site in PublicIpResolverServiceUrls)
            {
                try
                {
                    using var response = await client.GetAsync(site, cancellationToken);
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    ip = IPAddress.Parse(regex.Match(content).Value);
                    if (ip.IsPrivate())
                    {
                        continue;
                    }
                    break;
                }
                catch (TaskCanceledException) { canceled = true; }
                catch
                {
                    // ignore
                }
            }
        }

        lock (wanIpLock)
        {
            if (!canceled)
            {
                wanIpCache = ip;
            }

            return ip;
        }
    }

    /// <summary>
    /// Получить IP адреса установленных на компе VPN для объединения локальных адресов
    /// </summary>
    public static IEnumerable<(IPAddress Address, string NetworkName)> GetVpnIps(string[] vpnNetworkNames)
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!vpnNetworkNames.Contains(ni.Name, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (var ip in ni.GetIPProperties().UnicastAddresses)
            {
                var address = ip.Address.TryExtractMappedIPv4();
                if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
                {
                    continue;
                }
                if (address is { AddressFamily: AddressFamily.InterNetworkV6 } and ({ IsIPv6LinkLocal: true } or { IsIPv6Multicast: true }))
                {
                    continue;
                }
                yield return (address, ni.Name.Replace("VPN", "").Trim());
            }
        }
    }

    /// <summary>
    /// Получает поддерживаемый VPN-адрес, если он известен текущему компьютеру
    /// </summary>
    public static IEnumerable<(IPAddress Address, string NetworkName)> GetVpnIps() => GetVpnIps(SupportedVpnNames.ToArray());

    /// <summary>
    /// Получить список всех доступых Ip адресов
    /// </summary>
    public static async Task<IEnumerable<MachineKnownIp>> GetAllKnownIpsAsync(bool includeWan = true, bool includeVpns = true, CancellationToken cancellationToken = default)
    {
        var wantedIps = new List<Task>();

        var machineKnownIps = Task.Run(() => GetLocalMachineAttachedIpAddresses().ToArray(), cancellationToken);
        var wanIp = GetWanIpAsync(cancellationToken);

        if (includeWan)
        {
            wantedIps.Add(wanIp);
        }

        var vpnIps = Task.Run(GetVpnIps, cancellationToken);

        if (includeVpns)
        {
            wantedIps.Add(vpnIps);
        }

        await Task.WhenAll(wantedIps);

        List<MachineKnownIp> knownIps = [];
        foreach (var knownIp in await machineKnownIps)
        {
            if (knownIp.Address.IsPrivate())
            {
                knownIps.Add(new MachineKnownIp(knownIp.Address.TryExtractMappedIPv4(), AddressOrigin.Lan, knownIp.NetworkName));
            }
        }
        if (includeWan)
        {
            if (await wanIp is { } wanAddress && !wanAddress.IsPrivate())
            {
                knownIps.Add(new MachineKnownIp(wanAddress, AddressOrigin.Wan));
            }
        }
        if (includeVpns)
        {
            foreach ((var vpnAddress, var vpnName) in await vpnIps)
            {
                if (vpnAddress == null)
                {
                    continue;
                }
                knownIps.Add(new MachineKnownIp(vpnAddress.TryExtractMappedIPv4(), AddressOrigin.Vpn, vpnName));
            }
        }
        foreach (var knownIp in await machineKnownIps)
        {
            if (!knownIp.Address.IsPrivate() && !SupportedVpnNames.Where(x => x.Contains(knownIp.NetworkName ?? string.Empty)).Any())
            {
                knownIps.Add(new MachineKnownIp(knownIp.Address.TryExtractMappedIPv4(), AddressOrigin.Wan, knownIp.NetworkName));
            }
        }
        return knownIps.Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);
    }

    /// <summary>
    /// Имеет возможность выхода в интернет
    /// </summary>
    public static bool HasInternetConnectivity()
    {
        lock (connectivityLock)
        {
            if (hasInternet.HasValue && lastSeenPacketChange != -1 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastSeenPacketChange < TimeSpan.FromSeconds(5).TotalMilliseconds)
            {
                return hasInternet.Value;
            }
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return (hasInternet = false).Value;
            }
            var prevNetBytes = lastBytesSendOrReceived;
            var currentNetBytes = GetTotalInternetBytes();
            currentNetBytes = currentNetBytes < 1 ? -1 : currentNetBytes;
            hasInternet = prevNetBytes != currentNetBytes;

            lastBytesSendOrReceived = currentNetBytes;
            lastSeenPacketChange = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return hasInternet.Value;
        }

        static long GetTotalInternetBytes() =>
            GetInternetInterfaces().Sum(i =>
            {
                var stats = i.GetIPStatistics();
                return stats.BytesReceived + stats.BytesSent;
            });
    }

    /// <summary>
    /// Получает адреса, назначенные сетевым интерфейсам, подключенным к текущему компьютеру.
    /// </summary>
    private static IEnumerable<MachineKnownIp> GetLocalMachineAttachedIpAddresses()
    {
        HashSet<IPAddress> seenAddresses = [];
        foreach (var networkInterface in GetInternetInterfaces())
        {
            foreach (var ip in networkInterface.GetIPProperties().UnicastAddresses)
            {
                var address = ip.Address.TryExtractMappedIPv4();
                if (!IsIpForSharing(address) || HasSeen(address))
                {
                    continue;
                }

                yield return new MachineKnownIp(address, address.IsPrivate() ? AddressOrigin.Lan : AddressOrigin.Wan, networkInterface.Name);
            }
        }

        bool HasSeen(IPAddress address) => !seenAddresses.Add(address);

        static bool IsIpForSharing(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
            {
                return false;
            }
            return address switch
            {
                { AddressFamily: AddressFamily.InterNetwork } when address.Equals(IPAddress.Any) => false,
                { AddressFamily: AddressFamily.InterNetwork } when address.Equals(IPAddress.None) => false,
                { AddressFamily: AddressFamily.InterNetworkV6 } when address.Equals(IPAddress.IPv6Any) => false,
                { AddressFamily: AddressFamily.InterNetworkV6, IsIPv6LinkLocal: true } => false,
                { AddressFamily: AddressFamily.InterNetworkV6, IsIPv6Multicast: true } => false,
                _ => true
            };
        }
    }
}
