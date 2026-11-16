using MIN.Audio.Services.Contracts.Interfaces;

namespace MIN.Audio.DI.FeatureCollection;

/// <summary>
/// Набор функциональностей для Audio
/// </summary>
public interface IAudioFeatureCollection
{
    /// <inheritdoc cref="IAudioDeviceService"/>
    IAudioDeviceService AudioDeviceService { get; }
}
