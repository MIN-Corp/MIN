using MIN.Audio.Services.Contracts.Models;

namespace MIN.Audio.Services.Contracts.Interfaces;

/// <summary>
/// Помощник в выборе звуковое устройство ввода/вывода
/// </summary>
public interface IAudioDeviceService
{
    /// <summary>
    /// Получить микрофоны
    /// </summary>
    IReadOnlyList<AudioDeviceInfo> GetInputDevices(bool asDecoded = false);

    /// <summary>
    /// Получить динамики
    /// </summary>
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices(bool asDecoded = false);
}
