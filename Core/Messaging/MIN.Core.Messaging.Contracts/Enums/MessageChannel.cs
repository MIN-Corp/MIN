namespace MIN.Core.Messaging.Contracts.Enums;

/// <summary>
/// Канал, по которому пойдёт сообщение
/// </summary>
public enum MessageChannel
{
    /// <summary>
    /// Надёжный, гарантирует доставку (TCP). Используется для передачи большинства сообщений
    /// </summary>
    Secure,

    /// <summary>
    /// Быстрый, но не надёжный (UDP). Используется для стриминга большого кол-ва инфы.
    /// </summary>
    Fast
}
