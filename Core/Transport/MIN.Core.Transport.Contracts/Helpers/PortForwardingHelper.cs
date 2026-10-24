using System.Net;
using Open.Nat;

namespace MIN.Core.Transport.Contracts.Helpers;

/// <summary>
/// Результат проброски порта
/// </summary>
public enum ResultCodes
{
    /// <summary>
    /// Успех
    /// </summary>
    Success,

    /// <summary>
    /// Конфликт
    /// </summary>
    ConflictInMappingEntry,

    /// <summary>
    /// Неизвестная ошибка
    /// </summary>
    UnknownError
}

/// <summary>
/// Помошник в пробросе порта с использованием UPnP
/// </summary>
public static class PortForwardingHelper
{
    private readonly static List<ushort> mappedPorts = [];

    /// <summary>
    /// Получить внешний IP адрес от устройства
    /// </summary>
    public static async Task<IPAddress?> GetExternalIpAsync(CancellationToken cancellationToken)
    {
        var device = await OpenNatHelper.GetDeviceAsync(cancellationToken: cancellationToken);
        if (device == null)
        {
            return null;
        }

        return await device.GetExternalIPAsync();
    }

    /// <summary>
    /// Найти маршрутизатор и создать проброс порта
    /// </summary>
    /// <param name="port">Ваш локальный порт (например, 56784)</param>
    /// <param name="protocol">Протокол для маппинга (Tcp, Udp)</param>
    /// <param name="description">Описание для правила (например, "Game room fgj")</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Код результата</returns>
    public static async Task<ResultCodes?> MapPortAsync(ushort port, Protocol protocol, CancellationToken cancellationToken, string description = "MIN Room")
    {
        var device = await OpenNatHelper.GetDeviceAsync(cancellationToken: cancellationToken);
        if (device == null)
        {
            return ResultCodes.UnknownError;
        }

        try
        {
            if (await device.GetSpecificMappingAsync(protocol, port) != null)
            {
                try
                {
                    mappedPorts.Remove(port);
                    await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, port, port));
                }
                catch
                {
                    return ResultCodes.ConflictInMappingEntry;
                }
            }

            // 2. Создаём
            var mapping = new Mapping(protocol, port, port, 0, $"{description} {port}");
            await device.CreatePortMapAsync(mapping);
            mappedPorts.Add(port);
            return ResultCodes.Success;
        }
        catch
        {
            // UPnP не поддерживается, отключён или таймаут
            return ResultCodes.UnknownError;
        }
    }
    /// <summary>
    /// Удалить созданный проброс порта
    /// </summary>
    public static async Task<bool> UnmapPortAsync(ushort port, Protocol protocol, CancellationToken cancellationToken = default)
    {
        if (!mappedPorts.Remove(port))
        {
            return false;
        }

        var tries = 3;
        while (tries-- >= 0)
        {
            if (await TryRemoveAsync(port, protocol, cancellationToken))
            {
                return true;
            }
            await Task.Delay(250, cancellationToken);
        }
        return false;

        static async Task<bool> TryRemoveAsync(ushort port, Protocol protocol, CancellationToken cancellationToken)
        {
            var device = await OpenNatHelper.GetDeviceAsync(cancellationToken: cancellationToken);
            if (device == null)
            {
                return false;
            }

            try
            {
                await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, port, port));
                return true;
            }
            catch (MappingException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Удалить все маппинги порта
    /// </summary>
    public static async Task UnmapAllAsync(Protocol protocol)
    {
        foreach (var port in mappedPorts.ToList())
        {
            await UnmapPortAsync(port, protocol);
        }
    }
}
