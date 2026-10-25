using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Enums;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Services.Tunnels.Instances;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Factories;

/// <summary>
/// Фабрика экземпляров туннеля
/// </summary>
public class TunnelInstanceFactory : ISessionInstanceFactory
{
    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TunnelInstanceFactory"/>
    /// </summary>
    public TunnelInstanceFactory(IMessageRouter messageRouter,
        IEventBus eventBus,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.messageRouter = messageRouter;
        this.eventBus = eventBus;
        this.identityService = identityService;
        this.logger = logger;
    }

    bool ISessionInstanceFactory.CanCreate(Session session)
        => session.Kind == SessionKind.PortTunnel;

    ISessionInstance ISessionInstanceFactory.Create(SessionInstanceContext context)
        => context.Role == SessionInstanceRole.Server
            ? new TunnelServerInstance(context, messageRouter, eventBus, identityService, logger)
            : new TunnelClientInstance(context, messageRouter, eventBus, identityService, logger);
}
