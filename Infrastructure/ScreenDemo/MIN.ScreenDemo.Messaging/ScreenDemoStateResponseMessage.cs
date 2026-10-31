using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Ответ на запрос активных демонстраций экрана в комнате
/// </summary>
public sealed class ScreenDemoStateResponseMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoStateResponse;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Активные демонстрации в комнате
    /// </summary>
    public List<ScreenDemoStateEntry> Demos { get; set; } = [];
}
