using Microsoft.Extensions.DependencyInjection;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Common.Mvc;
using MIN.Common.Mvc.Extensions;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.ScreenDemo.DI.FeatureCollection;
using MIN.ScreenDemo.Handlers;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services;

namespace MIN.ScreenDemo.DI;

/// <summary>
/// Модуль регистрации зависимостей для ScreenDemo
/// </summary>
public class ScreenDemoModule : Module
{
    /// <inheritdoc />
    protected override void Load(IServiceCollection services)
    {
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessage, IScreenDemoMessagingAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, IScreenDemoHandlerAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableTo<IHostedService, ScreenDemoMonitor>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<OpenH264VideoCodec>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ScreenDemoStateService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ScreenDemoPlaybackService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ScreenDemoTransmitter>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ScreenDemoFeatureCollection>(ServiceLifetime.Singleton);
    }
}
