using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Запрос на начало демонстрации экрана
/// </summary>
public sealed class ScreenDemoStartRequestMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoStartRequest;

    /// <inheritdoc />
    public override bool IsPublic => false;
}
