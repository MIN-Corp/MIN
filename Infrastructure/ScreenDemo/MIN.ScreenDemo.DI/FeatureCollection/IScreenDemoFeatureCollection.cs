using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.DI.FeatureCollection;

/// <summary>
/// Набор функциональностей для ScreenDemo
/// </summary>
public interface IScreenDemoFeatureCollection
{
    /// <summary>
    /// Есть возможность демонстрации экрана (записи)
    /// </summary>
    bool CanShareScreen { get; }

    /// <inheritdoc cref="IScreenDemoStateService"/>
    IScreenDemoStateService ScreenDemoState { get; }

    /// <inheritdoc cref="IScreenDemoTransmitter"/>
    IScreenDemoTransmitter ScreenDemoTransmitter { get; }

    /// <inheritdoc cref="IScreenDemoPlaybackService"/>
    IScreenDemoPlaybackService ScreenDemoPlayback { get; }
}
