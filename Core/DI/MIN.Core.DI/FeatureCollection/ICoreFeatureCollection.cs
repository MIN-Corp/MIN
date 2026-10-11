using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Interfaces;

namespace MIN.Core.DI.FeatureCollection;

/// <summary>
/// Набор функциональностей для Core
/// </summary>
public interface ICoreFeatureCollection
{
    /// <inheritdoc cref="IRoomLifecycleManager"/>
    IRoomLifecycleManager Lifecycle { get; }

    /// <inheritdoc cref="IRoomFactory"/>
    IRoomFactory RoomFactory { get; }

    /// <inheritdoc cref="IRoomStore"/>
    IRoomStore RoomStore { get; }

    /// <inheritdoc cref="IRoomPersistenceService"/>
    IRoomPersistenceService RoomPersistence { get; }

    /// <inheritdoc cref="IEventBus"/>
    IEventBus EventBus { get; }

    /// <inheritdoc cref="IIdentityService"/>
    IIdentityService IdentityService { get; }
}
