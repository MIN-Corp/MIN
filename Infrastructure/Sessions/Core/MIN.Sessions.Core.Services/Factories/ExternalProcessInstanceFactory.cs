using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Enums;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Services.Instances;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Factories;

/// <summary>
/// Фабрика экземпляров сессий во внешних процессах
/// </summary>
public class ExternalProcessInstanceFactory : ISessionInstanceFactory
{
    private readonly ISessionProcessBridge processBridge;
    private readonly ISessionTransportFactory transportFactory;
    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;
    private readonly IRoomFactory roomFactory;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ExternalProcessInstanceFactory"/>
    /// </summary>
    public ExternalProcessInstanceFactory(ISessionProcessBridge processBridge,
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

    bool ISessionInstanceFactory.CanCreate(Session session)
        => session.Kind == SessionKind.Process;

    ISessionInstance ISessionInstanceFactory.Create(SessionInstanceContext context)
        => new ExternalProcessInstance(context, processBridge, transportFactory, messageRouter, eventBus, roomFactory, identityService, logger);
}
