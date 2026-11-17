using System.Collections.Concurrent;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models;

namespace MIN.Core.Stores.Factories;

/// <inheritdoc cref="IRoomFactory"/>
public sealed class RoomFactory : IRoomFactory
{
    private readonly IServiceProvider serviceProvider;
    private readonly ConcurrentDictionary<Guid, RoomContext> contexts = new();

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="RoomFactory"/>
    /// </summary>
    public RoomFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    RoomContext IRoomFactory.GetOrCreateContext(Guid roomId)
        => contexts.GetOrAdd(roomId, id => new RoomContext(id, serviceProvider));

    RoomContext? IRoomFactory.GetRoomContext(Guid roomId)
    {
        contexts.TryGetValue(roomId, out var context);
        return context;
    }

    bool IRoomFactory.TryGetContext(Guid roomId, out RoomContext? context)
        => contexts.TryGetValue(roomId, out context);

    void IRoomFactory.DestroyContext(Guid roomId)
    {
        if (contexts.TryRemove(roomId, out var context))
        {
            context.Dispose();
        }
    }

    IEnumerable<RoomContext> IRoomFactory.GetAllContexts() => contexts.Values;
}
