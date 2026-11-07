using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Unsupported;

/// <summary>
/// Заглушка захвата системного звука для платформ без поддержки loopback
/// </summary>
public class UnsupportedSystemAudioCaptureService : ISystemAudioCaptureService
{
    /// <inheritdoc />
    public event EventHandler<ScreenDemoAudioFrame>? FrameCaptured
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public void Start()
    {
        throw new PlatformNotSupportedException("Захват системного звука не поддерживается на этой платформе");
    }

    /// <inheritdoc />
    public void Stop() { }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() { }
}
