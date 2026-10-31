namespace MIN.ScreenDemo.Services.Contracts.Models;

/// <summary>
/// Закодированный кадр экрана (Annex-B H.264)
/// </summary>
public sealed record EncodedScreenFrame(bool IsKeyframe, byte[] Data);
