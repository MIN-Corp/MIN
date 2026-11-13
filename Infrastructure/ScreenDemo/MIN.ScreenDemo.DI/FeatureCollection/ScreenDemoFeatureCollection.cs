using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.DI.FeatureCollection;

/// <inheritdoc cref="IScreenDemoFeatureCollection"/>
public class ScreenDemoFeatureCollection : IScreenDemoFeatureCollection
{
    /// <inheritdoc />
    public bool CanShareScreen => OperatingSystem.IsWindows();

    /// <inheritdoc cref="IScreenDemoStateService"/>
    public IScreenDemoStateService ScreenDemoState { get; }

    /// <inheritdoc cref="IScreenDemoPlaybackService"/>
    public IScreenDemoPlaybackService ScreenDemoPlayback { get; }

    /// <inheritdoc cref="IScreenDemoTransmitter"/>
    public IScreenDemoTransmitter ScreenDemoTransmitter { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoFeatureCollection"/>
    /// </summary>
    public ScreenDemoFeatureCollection(IScreenDemoStateService screenDemoState,
        IScreenDemoPlaybackService screenDemoPlayback,
        IScreenDemoTransmitter screenDemoTransmitter)
    {
        ScreenDemoState = screenDemoState;
        ScreenDemoPlayback = screenDemoPlayback;
        ScreenDemoTransmitter = screenDemoTransmitter;
    }
}
