using MIN.Audio.Services.Contracts.Interfaces;

namespace MIN.Audio.DI.FeatureCollection;

/// <inheritdoc cref="IAudioFeatureCollection"/>
public class AudioFeatureCollection : IAudioFeatureCollection
{
    /// <inheritdoc cref="IAudioDeviceService"/>
    public IAudioDeviceService AudioDeviceService { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="AudioFeatureCollection"/>
    /// </summary>
    public AudioFeatureCollection(IAudioDeviceService audioDeviceService)
    {
        AudioDeviceService = audioDeviceService;
    }
}
