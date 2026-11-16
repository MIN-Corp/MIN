using MIN.Core.Services.Contracts.Events;

namespace MIN.Core.Services.Contracts.Interfaces.Pipeline;

/// <summary>
/// Обработчик пакетов потока
/// </summary>
public interface IStreamChunkHandler
{
    /// <summary>
    /// Являются ли расшифрованные данные пакетом потока
    /// </summary>
    bool CanHandle(ReadOnlySpan<byte> plainData);

    /// <summary>
    /// Обработать пакет потока
    /// </summary>
    Task HandleAsync(ReadOnlyMemory<byte> plainData, RoomRawMessageReceivedEventArgs e, CancellationToken cancellationToken);
}
