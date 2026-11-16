using MIN.Voice.Services.Contacts.Interfaces;

namespace MIN.Voice.DI.FeatureCollection;

/// <inheritdoc cref="IVoiceFeatureCollection"/>
public class VoiceFeatureCollection : IVoiceFeatureCollection
{
    /// <inheritdoc cref="IMuteService"/>
    public IMuteService MuteService { get; }

    /// <inheritdoc cref="IVoicePlaybackService"/>
    public IVoicePlaybackService VoicePlayback { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="VoiceFeatureCollection"/>
    /// </summary>
    public VoiceFeatureCollection(IMuteService muteService, IVoicePlaybackService voicePlayback)
    {
        MuteService = muteService;
        VoicePlayback = voicePlayback;
    }
}
