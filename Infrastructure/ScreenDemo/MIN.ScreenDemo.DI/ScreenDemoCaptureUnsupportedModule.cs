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
public class ScreenDemoCaptureUnsupportedModule  : Module
{
    /// <inheritdoc />
    protected override void Load(IServiceCollection services)
    {
        services.RegisterAsImplementedInterfaces<UnsupportedScreenCaptureService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<UnsupportedScreenSourceService>(ServiceLifetime.Singleton);
    }
}
