using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.Desktop.Infrastructure.Services;

/// <summary>
/// Отрисовка кадра экрана в WriteableBitmap (BGRA8888)
/// </summary>
static internal class ScreenFrameBlitter
{
    /// <summary>
    /// Создать битмап под размер кадра
    /// </summary>
    public static WriteableBitmap Create(ScreenFrame frame)
        => new(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Opaque);

    /// <summary>
    /// Совпадает ли размер битмапа с кадром
    /// </summary>
    public static bool IsSameSize(WriteableBitmap? bitmap, ScreenFrame frame)
        => bitmap != null && bitmap.PixelSize.Width == frame.Width && bitmap.PixelSize.Height == frame.Height;

    /// <summary>
    /// Можно ли отрисовать кадр (размеры валидны, данных достаточно)
    /// </summary>
    public static bool IsRenderable(ScreenFrame frame)
        => frame.Width >= 1 && frame.Height >= 1 && frame.Data.Length >= frame.Width * 4 * frame.Height;

    /// <summary>
    /// Скопировать кадр в битмап. ScreenFrame всегда плотный (stride = width * 4),
    /// целевой битмап может иметь паддинг — копируем построчно при расхождении
    /// </summary>
    public static void Blit(WriteableBitmap bitmap, ScreenFrame frame)
    {
        var sourceStride = frame.Width * 4;

        using var framebuffer = bitmap.Lock();
        var targetStride = framebuffer.RowBytes;

        if (targetStride == sourceStride)
        {
            Marshal.Copy(frame.Data, 0, framebuffer.Address, Math.Min(frame.Data.Length, sourceStride * frame.Height));
        }
        else
        {
            for (var y = 0; y < frame.Height; y++)
            {
                Marshal.Copy(frame.Data, y * sourceStride, IntPtr.Add(framebuffer.Address, y * targetStride), sourceStride);
            }
        }
    }
}
