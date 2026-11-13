using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Сообщение подтверждения доступа к демонстрации экрана.
/// Приходит и стартовавшему демонстрацию (NeedToAnnounce = false),
/// и присоединившемуся зрителю (NeedToAnnounce = true)
/// </summary>
public sealed class ScreenDemoEstablishedMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoEstablished;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; set; }

    /// <summary>
    /// Кто демонстрирует экран
    /// </summary>
    public Guid SharerId { get; set; }

    /// <summary>
    /// Флаг, указывающий, нужно ли оповещать остальных о входе зрителя
    /// </summary>
    public bool NeedToAnnounce { get; set; }
}
