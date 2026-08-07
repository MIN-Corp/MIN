using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.FastChannelConnect;

/// <summary>
/// Сообщение - запрос на подключение к быстрому каналу
/// </summary>
public sealed class FastChannelConnectRequestMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.FastChannelConnectRequest;

    /// <inheritdoc />
    public override bool IsPublic => false;
}
