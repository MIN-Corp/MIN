using Windows.UI.WebUI;
using Microsoft.Extensions.DependencyInjection;
using MIN.Common.Mvc;
using MIN.Common.Mvc.Extensions;

namespace MIN.ScreenDemo.Capture.Windows;

/// <summary>
/// Модуль регистрации платформенных (Windows) сервисов захвата экрана
/// </summary>
public class ScreenDemoCaptureWindowsModule : Module
{
    /// <inheritdoc />
    protected override void Load(IServiceCollection services)
    {
        services.RegisterAsImplementedInterfaces<WindowsGraphicsCaptureService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<WindowsScreenSourceService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<WindowsSystemAudioCaptureService>(ServiceLifetime.Singleton);
    }
}
