namespace MIN.ScreenDemo.Services.Contracts.Models;

/// <summary>
/// Кадр звука демонстрации (s16le stereo 48 kHz, AudioFrameDurationMs мс).
/// Массив принадлежит подписчику (обычное аллоцирование — как в Voice).
/// </summary>
public sealed record ScreenDemoAudioFrame(byte[] Data, long TimestampMs);
