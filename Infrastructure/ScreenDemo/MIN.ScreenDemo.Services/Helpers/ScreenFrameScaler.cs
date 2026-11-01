using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Helpers;

/// <summary>
/// Масштабировщик кадров экрана (nearest-neighbor — достаточно для экранного содержимого)
/// </summary>
static internal class ScreenFrameScaler
{
    /// <summary>
    /// Уменьшить кадр так, чтобы высота не превышала maxHeight (пропорции сохраняются,
    /// итоговые размеры чётные — требование кодека). Если кадр уже не больше — вернётся как есть
    /// </summary>
    public static ScreenFrame Downscale(ScreenFrame frame, int maxHeight)
    {
        if (frame.Height <= maxHeight)
        {
            return frame;
        }

        var newHeight = Math.Max(2, (int)Math.Round(maxHeight / 2d) * 2);
        var newWidth = Math.Max(2, (int)Math.Round(frame.Width * (newHeight / (double)frame.Height) / 2d) * 2);

        var sourceStride = frame.Width * 4;
        var targetStride = newWidth * 4;
        var target = new byte[targetStride * newHeight];

        for (var y = 0; y < newHeight; y++)
        {
            var sourceY = (int)(y * (long)frame.Height / newHeight);
            var sourceRow = sourceY * sourceStride;
            var targetRow = y * targetStride;

            for (var x = 0; x < newWidth; x++)
            {
                var sourceX = (int)(x * (long)frame.Width / newWidth);
                var sourcePixel = sourceRow + sourceX * 4;
                var targetPixel = targetRow + x * 4;

                target[targetPixel] = frame.Data[sourcePixel];
                target[targetPixel + 1] = frame.Data[sourcePixel + 1];
                target[targetPixel + 2] = frame.Data[sourcePixel + 2];
                target[targetPixel + 3] = frame.Data[sourcePixel + 3];
            }
        }

        return new ScreenFrame(newWidth, newHeight, target, frame.TimestampMs);
    }
}
