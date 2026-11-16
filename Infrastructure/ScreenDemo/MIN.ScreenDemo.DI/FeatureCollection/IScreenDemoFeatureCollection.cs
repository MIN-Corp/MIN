using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.DI.FeatureCollection;

/// <summary>
/// Набор функциональностей для ScreenDemo
/// </summary>
public interface IScreenDemoFeatureCollection
{
    /// <inheritdoc cref="IScreenDemoStateService"/>
    IScreenDemoStateService ScreenDemoState { get; }

    /// <inheritdoc cref="IScreenDemoTransmitter"/>
    IScreenDemoTransmitter ScreenDemoTransmitter { get; }

    /// <inheritdoc cref="IScreenDemoPlaybackService"/>
    IScreenDemoPlaybackService ScreenDemoPlayback { get; }
}
