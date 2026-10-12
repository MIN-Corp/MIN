using Microsoft.Extensions.DependencyInjection;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Common.Mvc;
using MIN.Common.Mvc.Extensions;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Sessions.Core.DI.FeatureCollection;
using MIN.Sessions.Core.Handlers;
using MIN.Sessions.Core.Messaging;
using MIN.Sessions.Core.Serialization.Json;
using MIN.Sessions.Core.Services;
using MIN.Sessions.Core.Transport;

namespace MIN.Sessions.Core.DI;

/// <summary>
/// Модуль регистрации зависимостей для Session
/// </summary>
public class SessionModule : Module
{
    /// <inheritdoc />
    protected override void Load(IServiceCollection services)
    {
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessage, ISessionsMessagingAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, ISessionsHandlerAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableTo<IHostedService, SessionMonitor>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<SessionProcessBridge>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<SessionFeatureCollection>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<SessionInstanceManager>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<SessionTransportFactory>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<SessionScanner>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<IpcJsonSerializer>(ServiceLifetime.Singleton);
    }
}
