using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Запрос на получение активных демонстраций экрана в комнате
/// </summary>
public sealed class ScreenDemoStateRequestMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoStateRequest;

    /// <inheritdoc />
    public override bool IsPublic => false;
}
