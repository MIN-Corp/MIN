using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.DI.FeatureCollection;

/// <inheritdoc cref="IScreenDemoFeatureCollection"/>
public class ScreenDemoFeatureCollection : IScreenDemoFeatureCollection
{
    /// <inheritdoc cref="IScreenDemoStateService"/>
    public IScreenDemoStateService ScreenDemoState { get; }

    /// <inheritdoc cref="IScreenDemoPlaybackService"/>
    public IScreenDemoPlaybackService ScreenDemoPlayback { get; }

    /// <inheritdoc cref="IScreenSourceService"/>
    public IScreenSourceService ScreenSources { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoFeatureCollection"/>
    /// </summary>
    public ScreenDemoFeatureCollection(IScreenDemoStateService screenDemoState,
        IScreenDemoPlaybackService screenDemoPlayback,
        IScreenSourceService screenSources)
    {
        ScreenDemoState = screenDemoState;
        ScreenDemoPlayback = screenDemoPlayback;
        ScreenSources = screenSources;
    }
}
