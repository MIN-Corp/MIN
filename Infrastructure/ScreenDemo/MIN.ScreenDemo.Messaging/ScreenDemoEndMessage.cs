using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Сообщение о завершении демонстрации экрана.
/// Рассылается самим демонстрирующим: хост деактивирует подкомнату,
/// зрители закрывают окна просмотра
/// </summary>
public sealed class ScreenDemoEndMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoEnd;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; set; }
}
