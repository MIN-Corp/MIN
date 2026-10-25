using System.ComponentModel;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces.SettingsServices;
using MIN.Helpers.Contracts.Models;
using MIN.Voice.Services.Contacts.Interfaces;
using MIN.Voice.Services.Models;

namespace MIN.Voice.Services;

/// <inheritdoc cref="IVoicePlaybackService"/>
public class VoicePlaybackService : IVoicePlaybackService
{
    private readonly Dictionary<Guid, ParticipantVoiceEntry> channels = [];
    private readonly IVoiceCodec codec;
    private readonly IAudioDeviceService audioDeviceService;
    private readonly ISettingsProvider settingsProvider;
    private readonly ILoggerProvider logger;
    private readonly object sync = new();
    private PlaybackDeviceContext? deviceContext;
    private float appVolume;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="VoicePlaybackService"/>
    /// </summary>
    public VoicePlaybackService(IVoiceCodec codec,
        IAudioDeviceService audioDeviceService,
        ISettingsProvider settingsProvider,
        ILoggerProvider logger)
    {
        this.codec = codec;
        this.audioDeviceService = audioDeviceService;
        this.settingsProvider = settingsProvider;
        this.logger = logger;

        settingsProvider.OnSettingsSaved += () =>
        {
            lock (sync)
            {
                var currentSettings = settingsProvider.GetSettings();
                appVolume = currentSettings.OutputDeviceVolume / 100.0f;

                var outputDevices = audioDeviceService.GetOutputDevices();
                if (currentSettings.OutputDeviceNumber < 0 || currentSettings.OutputDeviceNumber >= outputDevices.Count)
                {
                    return;
                }

                var device = outputDevices[currentSettings.OutputDeviceNumber];

                try
                {
                    deviceContext?.ChangeDevice(device.Name);
                }
                catch (Exception ex)
                {
                    logger.Log($"Не удалось сменить устройство воспроизведения: {ex.Message}");
                }
            }
        };
    }

    private PlaybackDeviceContext? EnsureDeviceContext()
    {
        if (deviceContext != null)
        {
            return deviceContext;
        }

        var settings = settingsProvider.GetSettings();
        appVolume = settings.OutputDeviceVolume / 100.0f;

        try
        {
            var outputDevices = audioDeviceService.GetOutputDevices();
            var deviceName = settings.OutputDeviceNumber >= 0 && settings.OutputDeviceNumber < outputDevices.Count
                ? outputDevices[settings.OutputDeviceNumber].Name
                : null;

            deviceContext = new PlaybackDeviceContext(deviceName);
            return deviceContext;
        }
        catch (Exception ex)
        {
            logger.Log($"Не удалось инициализировать устройство воспроизведения: {ex.Message}");
        }
        return null;
    }

    /// <inheritdoc />
    public void AddParticipant(Guid participantId)
    {
        lock (sync)
        {
            if (channels.ContainsKey(participantId))
            {
                return;
            }

            var context = EnsureDeviceContext();
            if (context == null)
            {
                logger.Log($"Не удалось создать канал воспроизведения для {participantId}: устройство воспроизведения не инициализировано");
                return;
            }

            var settings = settingsProvider.GetSettings();
            appVolume = settings.OutputDeviceVolume / 100.0f;

            try
            {
                var channel = new ParticipantChannel(codec, context, appVolume);

                void VolumeHandler(object? _, PropertyChangedEventArgs e)
                {
                    if (e.PropertyName == nameof(Settings.OutputDeviceVolume))
                    {
                        appVolume = settings.OutputDeviceVolume / 100.0f;
                        channel.ChangeVolume(appVolume, channel.SpecificVolume);
                    }
                }
                settings.PropertyChanged += VolumeHandler;

                channels[participantId] = new ParticipantVoiceEntry(channel, VolumeHandler);
            }
            catch (Exception ex)
            {
                logger.Log($"Не удалось создать канал воспроизведения для {participantId}: {ex.Message}");
            }
        }
    }

    void IVoicePlaybackService.RemoveParticipant(Guid participantId)
    {
        lock (sync)
        {
            if (!channels.Remove(participantId, out var entry))
            {
                return;
            }

            settingsProvider.GetSettings().PropertyChanged -= entry.VolumeHandler;
            entry.Channel.Dispose();
        }
    }

    void IVoicePlaybackService.ChangeParticipantVolume(Guid participantId, int specificVolume)
    {
        ParticipantChannel? channel;
        lock (sync)
        {
            channels.TryGetValue(participantId, out var entry);
            channel = entry?.Channel;
        }

        channel?.ChangeVolume(appVolume, specificVolume / 100.0f);
    }

    void IVoicePlaybackService.PlaySamples(Guid participantId, long sequenceNumber, byte[] data)
    {
        try
        {
            ParticipantChannel? channel;
            lock (sync)
            {
                EnsureDeviceContext();
                channels.TryGetValue(participantId, out var entry);
                channel = entry?.Channel;
            }

            channel?.Enqueue(sequenceNumber, data);
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка воспроизведения голоса для {participantId}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (sync)
        {
            var settings = settingsProvider.GetSettings();

            foreach (var entry in channels.Values)
            {
                settings.PropertyChanged -= entry.VolumeHandler;
                entry.Channel.Dispose();
            }

            channels.Clear();
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        Clear();
        deviceContext?.Dispose();
    }
}
