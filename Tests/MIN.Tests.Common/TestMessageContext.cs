using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Stores.Contracts.Models;

namespace MIN.Tests.Common;

/// <summary>
/// Фабрика контекстов обработки сообщений
/// </summary>
public static class TestMessageContext
{
    /// <summary>
    /// Создать контекст обработки сообщения
    /// </summary>
    public static MessageContext Create(RoomContext roomContext, Guid selfId, Guid connectionId, Role role, CancellationToken cancellationToken = default)
        => new(roomContext, selfId, connectionId, role, cancellationToken);
}
