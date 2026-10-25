using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Services.Contracts.Interfaces.Messaging;

/// <summary>
/// Маршрутизатор сообщений
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// Направить сообщение в зависимости от контекста
    /// </summary>
    Task RouteAsync(IMessage message, Guid roomId, Guid senderId,
        CancellationToken cancellationToken = default, IEnumerable<Guid>? broadcastExcludeIds = null);

    /// <summary>
    /// Оппубликовать сообщения для себя локально
    /// </summary>
    Task PublishLocally(IMessage message, Guid roomId, Role role,
        IEnumerable<Guid>? broadcastExcludeIds = null, CancellationToken cancellationToken = default);
}
