using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services;

/// <inheritdoc cref="IScreenCaptureService"/> для платформ, не способных к демонстрации экрана
public class UnsupportedScreenCaptureService : IScreenCaptureService
{
    /// <inheritdoc/>
    public event EventHandler<ScreenFrame>? FrameCaptured
    {
        add { }
        remove { }
    }

    void IScreenCaptureService.Start(ScreenSourceInfo source) => throw new PlatformNotSupportedException();

    void IScreenCaptureService.Stop()
    {
        // ignored
    }

    void IDisposable.Dispose()
    {
        // ignored
    }
}
