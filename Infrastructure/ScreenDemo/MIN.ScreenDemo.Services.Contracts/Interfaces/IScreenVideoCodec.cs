using MIN.ScreenDemo.Services.Contracts.Enums;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Interfaces;

/// <summary>
/// Кодировщик видео (экрана) в байты
/// </summary>
public interface IScreenVideoCodec : IDisposable
{
    /// <summary>
    /// Вид кодировки
    /// </summary>
    ScreenVideoCodecKind Kind { get; }

    /// <summary>
    /// Настроить кодек под размер кадра и битрейт.
    /// Вызывается перед началом кодирования и повторно при изменении размера кадра
    /// (например, при изменении размера захваченного окна)
    /// </summary>
    void Configure(int width, int height, int targetBitrateKbps);

    /// <summary>
    /// Закодировать кадр в байты (Annex-B)
    /// </summary>
    /// <param name="frame">Несжатый кадр (BGRA)</param>
    /// <param name="isKeyframe">Является ли закодированный кадр ключевым</param>
    byte[] Encode(ScreenFrame frame, out bool isKeyframe);

    /// <summary>
    /// Распаковать байты в кадр (BGRA)
    /// </summary>
    ScreenFrame Decode(byte[] encoded, long timestampMs);
}
