using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Фрагмент закодированного кадра демонстрации экрана.
/// Кадр делится на фрагменты, т.к. Закодированный кадр может не влезать
/// в один UDP-пакет. Сборка кадра происходит на стороне зрителя,
/// недокадры отбрасываются до следующего ключевого кадра
/// </summary>
public sealed class ScreenDemoFrameDataMessage : BaseMessage, IWithinSubRoom
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoFrameData;

    /// <inheritdoc />
    public override MessageChannel Channel => MessageChannel.Fast;

    /// <inheritdoc />
    public override bool RequireStreamAcks => false;

    /// <summary>
    /// Локальная дупликация не нужна: предпросмотр демонстрирующий
    /// получает напрямую из захвата, до кодирования
    /// </summary>
    public override bool RequiresLocalDuplication => false;

    /// <inheritdoc />
    public int SubRoomId { get; set; }

    /// <summary>
    /// Идентификатор кадра в рамках потока демонстрации
    /// </summary>
    public long FrameId { get; init; }

    /// <summary>
    /// Индекс фрагмента кадра (с нуля)
    /// </summary>
    public int FragmentIndex { get; init; }

    /// <summary>
    /// Общее количество фрагментов кадра
    /// </summary>
    public int FragmentCount { get; init; }

    /// <summary>
    /// Является ли кадр ключевым
    /// </summary>
    public bool IsKeyframe { get; init; }

    /// <summary>
    /// Содержимое фрагмента (кусок H.264 Annex-B)
    /// </summary>
    public required byte[] Data { get; init; }
}
