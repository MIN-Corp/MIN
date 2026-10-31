using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Сообщение о завершении демонстрации экрана.
/// Рассылается самим демонстрирующим: хост деактивирует подкомнату,
/// зрители закрывают окна просмотра
/// </summary>
public sealed class ScreenDemoEndMessage : BaseMessage, IWithinSubRoom
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoEnd;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <inheritdoc />
    public int SubRoomId { get; set; }
}
