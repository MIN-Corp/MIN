using MIN.Voice.Services.Contacts.Interfaces;

namespace MIN.Voice.DI.FeatureCollection;

/// <summary>
/// Набор функциональностей для Voice
/// </summary>
public interface IVoiceFeatureCollection
{
    /// <inheritdoc cref="IMuteService"/>
    IMuteService MuteService { get; }

    /// <inheritdoc cref="IVoicePlaybackService"/>
    IVoicePlaybackService VoicePlayback { get; }
}
