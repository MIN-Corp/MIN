using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.Transport.Contracts.Interfaces;

namespace MIN.Core.Messaging.Stateless.FastChannelConnect;

/// <summary>
/// Сообщение - ответ на подключение к быстрому каналу
/// </summary>
public sealed class FastChannelConnectResponseMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.FastChannelConnectResponse;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Точка подключения к быстрому каналу
    /// </summary>
    public IEndpoint FastChannelEndpoint { get; set; } = null!;
}
