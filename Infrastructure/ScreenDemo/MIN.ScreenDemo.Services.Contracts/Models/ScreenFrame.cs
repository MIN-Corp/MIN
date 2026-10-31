namespace MIN.ScreenDemo.Services.Contracts.Models;

/// <summary>
/// Несжатый кадр экрана (BGRA, 4 байта на пиксель, stride = Width * 4)
/// </summary>
public sealed record ScreenFrame(int Width, int Height, byte[] Data, long TimestampMs);
