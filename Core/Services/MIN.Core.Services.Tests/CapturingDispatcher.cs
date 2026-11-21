using System.Collections.Concurrent;
using MIN.Core.Handlers.Contracts.Dispatcher;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Services.Tests;

/// <summary>
/// Диспетчер, сохраняющий все переданные сообщения для проверок
/// </summary>
public sealed class CapturingDispatcher : IMessageDispatcher
{
    /// <summary>
    /// Диспетчеризованные сообщения с контекстами
    /// </summary>
    public ConcurrentQueue<(IMessage Message, MessageContext Context)> Dispatched { get; } = new();

    /// <inheritdoc />
    public Task DispatchAsync(IMessage message, MessageContext context, IEnumerable<Guid>? broadcastExcludeIds = null)
    {
        Dispatched.Enqueue((message, context));
        return Task.CompletedTask;
    }
}
