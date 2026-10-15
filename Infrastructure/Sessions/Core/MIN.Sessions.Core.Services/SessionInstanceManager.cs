using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Services.Instances;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services;

/// <summary>
/// Менеджер экземпляров сессий
/// </summary>
public class SessionInstanceManager : ISessionInstanceManager
{
    private readonly Dictionary<SessionInstanceContext, ISessionInstance> instances = [];
    private readonly ISessionProcessBridge processBridge;
    private readonly ISessionTransportFactory transportFactory;
    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;
    private readonly IRoomFactory roomFactory;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SessionInstanceManager"/>
    /// </summary>
    public SessionInstanceManager(ISessionProcessBridge processBridge,
        ISessionTransportFactory transportFactory,
        IMessageRouter messageRouter,
        IEventBus eventBus,
        IRoomFactory roomFactory,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.processBridge = processBridge;
        this.transportFactory = transportFactory;
        this.messageRouter = messageRouter;
        this.eventBus = eventBus;
        this.roomFactory = roomFactory;
        this.identityService = identityService;
        this.logger = logger;
    }

    async Task<ISessionInstance?> ISessionInstanceManager.StartAsync(Session session, SessionInstanceContext context, CancellationToken ct)
    {
        if (instances.ContainsKey(context))
        {
            logger.Log($"Экземпляр сессии {session.Name} для {context.RoomId}/{context.SubRoomId}/{context.Role} уже запущен", LogLevel.Warning);
            return null;
        }

        var instance = CreateInstance(session, context);
        instances[context] = instance;
        instance.Ended += async (_, _) =>
        {
            instances.Remove(context);
            await instance.DisposeAsync().ConfigureAwait(false);
        };

        try
        {
            await instance.StartAsync(session, ct).ConfigureAwait(false);
            return instance;
        }
        catch (Exception ex)
        {
            logger.Log($"Не удалось запустить экземпляр сессии {session.Name}: {ex.Message}", LogLevel.Error);
            instances.Remove(context);
            await instance.StopAsync(CancellationToken.None).ConfigureAwait(false);
            await instance.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    async Task ISessionInstanceManager.StopAsync(SessionInstanceContext context)
    {
        if (!instances.Remove(context, out var instance))
        {
            return;
        }

        await instance.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await instance.DisposeAsync().ConfigureAwait(false);
    }

    async Task ISessionInstanceManager.StopForRoomAsync(Guid roomId)
    {
        foreach (var context in instances.Keys.Where(x => x.RoomId == roomId).ToList())
        {
            await ((ISessionInstanceManager)this).StopAsync(context).ConfigureAwait(false);
        }
    }

    async Task ISessionInstanceManager.StopAllAsync()
    {
        foreach (var context in instances.Keys.ToList())
        {
            await ((ISessionInstanceManager)this).StopAsync(context).ConfigureAwait(false);
        }
    }

    bool ISessionInstanceManager.SessionClientAppExists(Session session)
        => Path.Exists(session.GetClientPath());

    IEnumerable<ISessionInstance> ISessionInstanceManager.GetInstances(Guid roomId, int subRoomId)
        => instances.Keys
            .Where(x => x.RoomId == roomId && x.SubRoomId == subRoomId)
            .Select(x => instances[x])
            .ToList();

    /// <summary>
    /// Создать экземпляр сессии (точка расширения для внутренних сервисов, например туннеля)
    /// </summary>
    protected virtual ISessionInstance CreateInstance(Session session, SessionInstanceContext context)
        => new ExternalProcessInstance(context, processBridge, transportFactory, messageRouter, eventBus, roomFactory, identityService, logger);
}
