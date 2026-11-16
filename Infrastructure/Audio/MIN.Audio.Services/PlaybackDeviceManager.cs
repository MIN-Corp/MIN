using MIN.Audio.Services.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces.SettingsServices;

namespace MIN.Audio.Services;

/// <summary>
/// Владеет ЕДИНСТВЕННЫМ на приложение <see cref="PlaybackDeviceContext"/>:
/// создаёт его лениво по настройкам, меняет устройство при сохранении настроек
/// и логирует ошибки. Все потребители (голос, демонстрация экрана) получают
/// один и тот же device/context — повторное создание недопустимо, т.к.
/// "Текущий контекст" OpenAL глобален на процесс.
/// </summary>
public sealed class PlaybackDeviceManager : IDisposable
{
    private readonly IAudioDeviceService audioDeviceService;
    private readonly ISettingsProvider settingsProvider;
    private readonly ILoggerProvider logger;
    private readonly object sync = new();
    private PlaybackDeviceContext? context;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="PlaybackDeviceManager"/>
    /// </summary>
    public PlaybackDeviceManager(IAudioDeviceService audioDeviceService,
        ISettingsProvider settingsProvider,
        ILoggerProvider logger)
    {
        this.audioDeviceService = audioDeviceService;
        this.settingsProvider = settingsProvider;
        this.logger = logger;

        settingsProvider.OnSettingsSaved += OnSettingsSaved;
    }

    /// <summary>
    /// Возвращает общий контекст воспроизведения, создавая его при первом обращении.
    /// Null — если устройство воспроизведения недоступно (не крашит приложение).
    /// </summary>
    public PlaybackDeviceContext? GetOrCreate()
    {
        lock (sync)
        {
            if (context != null)
            {
                return context;
            }

            try
            {
                context = new PlaybackDeviceContext(ResolveDeviceName());
                return context;
            }
            catch (Exception ex)
            {
                logger.Log($"Не удалось инициализировать устройство воспроизведения: {ex.Message}");
                return null;
            }
        }
    }

    private void OnSettingsSaved()
    {
        lock (sync)
        {
            // Контекста ещё нет — при первом GetOrCreate возьмём свежие настройки
            if (context == null)
            {
                return;
            }

            try
            {
                context.ChangeDevice(ResolveDeviceName());
            }
            catch (Exception ex)
            {
                logger.Log($"Не удалось сменить устройство воспроизведения: {ex.Message}");
            }
        }
    }

    private string? ResolveDeviceName()
    {
        var settings = settingsProvider.GetSettings();
        var outputDevices = audioDeviceService.GetOutputDevices();

        return settings.OutputDeviceNumber >= 0 && settings.OutputDeviceNumber < outputDevices.Count
            ? outputDevices[settings.OutputDeviceNumber].Name
            : null;
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        lock (sync)
        {
            settingsProvider.OnSettingsSaved -= OnSettingsSaved;
            context?.Dispose();
            context = null;
        }
    }
}
