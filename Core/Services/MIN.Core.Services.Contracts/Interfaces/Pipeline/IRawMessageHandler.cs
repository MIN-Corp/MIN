using MIN.Core.Services.Contracts.Events;
using MIN.Core.Streaming.Contracts.Events.Receiving;

namespace MIN.Core.Services.Contracts.Interfaces.Pipeline;

/// <summary>
/// Обработчик обычных сообщений: дешифровка, десериализация, диспатч
/// </summary>
public interface IRawMessageHandler
{
    /// <summary>
    /// Расшифровать входящие данные
    /// </summary>
    /// <returns>
    /// Null - данные следует отбросить (неизвестный отправитель)
    /// </returns>
    ReadOnlyMemory<byte>? TryDecrypt(RoomRawMessageReceivedEventArgs e);

    /// <summary>
    /// Десериализовать и отправить в диспетчер обычное сообщение
    /// </summary>
    Task HandleRawAsync(RoomRawMessageReceivedEventArgs e, ReadOnlyMemory<byte> plainData, CancellationToken cancellationToken);

    /// <summary>
    /// Десериализовать и отправить в диспетчер сообщение, собранное из пакетов потока
    /// </summary>
    Task HandleAssembledAsync(MessageAssembledEventArgs e, CancellationToken cancellationToken);
}
