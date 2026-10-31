namespace MIN.ScreenDemo.Services.Contracts.Models;

/// <summary>
/// Аргументы события декодирования кадра демонстрации экрана
/// </summary>
public sealed class ScreenDemoFrameDecodedEventArgs : EventArgs
{
    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор участника, который демонстрирует экран
    /// </summary>
    public Guid SharerId { get; init; }

    /// <summary>
    /// Декодированный кадр (BGRA)
    /// </summary>
    public required ScreenFrame Frame { get; init; }
}
