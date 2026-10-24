using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MIN.Desktop.Infrastructure.Services;

/// <summary>
/// Помошник в парсировании ip адреса на адрес и порт
/// </summary>
public static partial class IpAddressParser
{
    [GeneratedRegex("^[a-zA-Z0-9]([a-zA-Z0-9\\-]{0,61}[a-zA-Z0-9])?(\\.[a-zA-Z0-9]([a-zA-Z0-9\\-]{0,61}[a-zA-Z0-9])?)+$")]
    private static partial Regex DnsResolveRegex();

    /// <summary>
    /// Распарсить ip адрес на адрес и порт
    /// </summary>
    public static bool TryParseIpAddress(string? ipAddressAndPort, out string ipAddress, out int port)
    {
        ipAddress = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(ipAddressAndPort))
        {
            return false;
        }

        var parts = ipAddressAndPort.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var parsedPort) || parsedPort <= 0 || parsedPort > 65535)
        {
            return false;
        }

        var addr = parts[0];

        if (!IPAddress.TryParse(addr, out _) || addr.Split('.').Length != 4)
        {
            return false;
        }

        port = parsedPort;
        ipAddress = addr;
        return true;
    }

    /// <summary>
    /// Валидировать IP адрес и вернуть ip адрес в случае DNS резолвинга
    /// </summary>
    public static async Task<string> ValidateIp(string ipAddress)
    {
        if (IPAddress.TryParse(ipAddress, out _) && ipAddress.Split('.').Length == 4)
        {
            return ipAddress;
        }

        return DnsResolveRegex().IsMatch(ipAddress)
            ? await ResolveDns(ipAddress)
            : throw new InvalidOperationException("IP Адрес задан в неккоретном формате");
    }


    private static async Task<string> ResolveDns(string hostname)
    {
        try
        {
            var entry = await Dns.GetHostAddressesAsync(hostname);
            return entry.Length == 0
                ? throw new InvalidOperationException("IP Адрес задан в неккоретном формате")
                : entry.First().ToString();
        }
        catch
        {
            throw new InvalidOperationException("DNS не смог распознать IP адрес");
        }
    }
}
