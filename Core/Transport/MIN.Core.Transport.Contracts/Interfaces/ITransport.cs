using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Models;

namespace MIN.Core.Transport.Contracts.Interfaces;

/// <summary>
/// Интерфейс транспортного уровня для передачи данных между устройствами
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Событие получения сырых данных от транспорта
    /// </summary>
    event EventHandler<RawMessageReceivedEventArgs>? RawMessageReceived;

    /// <summary>
    /// Событие изменения состояния соединения
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Отправить сырые данные соединению
    /// </summary>
    /// <remarks>
    /// serverConnectionId указывает, какой id соединения у сервера в случае хоста
    /// </remarks>
    Task SendAsync(byte[] data, Guid receipientConnectionId, Guid? serverConnectionId, MessageChannel channel, CancellationToken cancellationToken);

    /// <summary>
    /// Отправить сырые данные всем соединениям
    /// </summary>
    Task BroadcastAsync(byte[] data, Guid connectionId, IEnumerable<Guid>? excludeConnections, MessageChannel channel, CancellationToken cancellationToken);

    /// <summary>
    /// Запустить сервер подключений (с желаемым id)
    /// </summary>
    Task<Guid> StartHostingAsync(Guid? serverConnectionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Настроить доступ и получить все точки подключения
    /// </summary>
    /// <remarks>
    /// Настраивает только PortForwarding и vpn
    /// </remarks>
    Task<IEnumerable<IEndpoint>> SetUpEndpoints(Guid connectionId, NetworkOptions networkOptions, NetworkOptions? oldNetworkOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить последние сохранённые точки подключения
    /// </summary>
    IEnumerable<IEndpoint> GetEndpoints(Guid serverConnectionId);

    /// <summary>
    /// Прекратить сервер для указанного соединения
    /// </summary>
    Task StopHostingAsync(Guid connectionId);

    /// <summary>
    /// Подключиться к удалённому устройству (с желаемым id)
    /// </summary>
    Task<Guid> ConnectAsync(IEndpoint endpoint, Guid? connectionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Разорвать соединение с указанным соединением
    /// </summary>
    Task DisconnectClientAsync(Guid clientConnectionId, Guid? serverConnectionId, DisconnectReason reason = DisconnectReason.None);

    /// <summary>
    /// Отключиться
    /// </summary>
    Task DisconnectAsync(Guid connectionId, DisconnectReason reason = DisconnectReason.None);
}
