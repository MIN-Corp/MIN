using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Interfaces;

namespace MIN.Core.DI.FeatureCollection;

/// <inheritdoc cref="ICoreFeatureCollection"/>
public class CoreFeatureCollection : ICoreFeatureCollection
{
    /// <inheritdoc cref="IRoomLifecycleManager"/>
    public IRoomLifecycleManager Lifecycle { get; }

    /// <inheritdoc cref="IRoomFactory"/>
    public IRoomFactory RoomFactory { get; }

    /// <inheritdoc cref="IRoomStore"/>
    public IRoomStore RoomStore { get; }

    /// <inheritdoc cref="IRoomPersistenceService"/>
    public IRoomPersistenceService RoomPersistence { get; }

    /// <inheritdoc cref="IEventBus"/>
    public IEventBus EventBus { get; }

    /// <inheritdoc cref="IIdentityService"/>
    public IIdentityService IdentityService { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CoreFeatureCollection"/>
    /// </summary>
    public CoreFeatureCollection(IRoomLifecycleManager lifecycle,
        IRoomFactory roomFactory,
        IRoomStore roomStore,
        IRoomPersistenceService roomPersistence,
        IEventBus eventBus,
        IIdentityService identityService)
    {
        Lifecycle = lifecycle;
        RoomFactory = roomFactory;
        RoomStore = roomStore;
        RoomPersistence = roomPersistence;
        EventBus = eventBus;
        IdentityService = identityService;
    }
}
