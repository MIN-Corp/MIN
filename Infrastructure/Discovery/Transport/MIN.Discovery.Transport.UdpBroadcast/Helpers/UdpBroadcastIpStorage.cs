using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Discovery.Transport.UdpBroadcast.Helpers;

/// <summary>
/// Хелпер для сохранения широковещательных каналов в локальной сети
/// </summary>
internal class UdpBroadcastIpStorage : IDisposable
{
    private readonly string addressesPath;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly SemaphoreSlim localKeyLock = new(1, 1);
    private bool disposed;

    public UdpBroadcastIpStorage(IAppDataProvider appDataProvider)
    {
        var directory = Directory.CreateDirectory(Path.Combine(appDataProvider.VersionedDirectory, "network")).FullName;
        addressesPath = Path.Combine(directory, "broadcastAddreses.json");

        jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<IEnumerable<IPAddress>?> LoadBroadcastAddressesAsync(CancellationToken cancellationToken = default)
    {
        await localKeyLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(addressesPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(addressesPath, cancellationToken);
            return JsonSerializer.Deserialize<string[]>(json, jsonOptions)?.Select(IPAddress.Parse);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Broadcast addresses file is corrupted", ex);
        }
        finally
        {
            localKeyLock.Release();
        }
    }

    public async Task SaveBroadcastAddressesAsync(IEnumerable<IPAddress> iPAddresses, CancellationToken cancellationToken = default)
    {
        await localKeyLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(iPAddresses.Select(x => x.ToString()).ToArray(), jsonOptions);
            await File.WriteAllTextAsync(addressesPath, json, cancellationToken);
        }
        finally
        {
            localKeyLock.Release();
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        localKeyLock.Dispose();
        disposed = true;
    }
}
