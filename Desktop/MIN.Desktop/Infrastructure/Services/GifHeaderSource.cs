using System;
using System.IO;
using Avalonia;
using Avalonia.Labs.Gif;

namespace MIN.Desktop.Infrastructure.Services;

/// <summary>
/// IGifSource, читающий только заголовок GIF (10 байт) — без полного парсинга файла.
/// Полный разбор произойдёт один раз при первом показе анимации контролом GifImage.
/// </summary>
public sealed class GifHeaderSource : IGifSource, IDisposable
{
    private readonly Stream stream;

    private GifHeaderSource(Stream stream, PixelSize size)
    {
        this.stream = stream;
        Size = size;
    }

    /// <inheritdoc />
    public PixelSize Size { get; }

    /// <inheritdoc />
    public Stream GetStream()
    {
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Обернуть поток GIF, прочитав только заголовок
    /// </summary>
    public static GifHeaderSource FromStream(Stream stream)
    {
        Span<byte> header = stackalloc byte[10];

        if (stream.Read(header) < 10
            || header[0] != (byte)'G' || header[1] != (byte)'I' || header[2] != (byte)'F')
        {
            throw new ArgumentException("Поток не является GIF (нет сигнатуры GIF87a/GIF89a)");
        }

        var width = header[6] | (header[7] << 8);
        var height = header[8] | (header[9] << 8);

        return new GifHeaderSource(stream, new PixelSize(width, height));
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => stream.Dispose();
}
